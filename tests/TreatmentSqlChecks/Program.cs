using System.Security.Claims;
using COSGeniusSystem.Common;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.DTOs.Treatments;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using COSGeniusSystem.Services.Examinations;
using COSGeniusSystem.Services.Treatments;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// No production configuration, migrations or schema creation. Fixtures remain for inspection.
var connection = Environment.GetEnvironmentVariable("GENIUS_TREATMENT_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(connection)) { Console.WriteLine("SKIPPED: dedicated V2 test connection required."); return 2; }
var builder = new SqlConnectionStringBuilder(connection);
if (!builder.InitialCatalog.StartsWith("GeniusTreatmentTest_", StringComparison.Ordinal)
    || Environment.GetEnvironmentVariable("GENIUS_TREATMENT_ALLOW_TEST_WRITES") != builder.InitialCatalog)
    throw new InvalidOperationException("Refusing writes outside an explicitly allowed GeniusTreatmentTest_ database.");
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Treatments:RequireFullPayment"] = "true" }).Build();
var ct = CancellationToken.None;
var checks = 0;
void Check(bool success, string message) { if (!success) throw new Exception("FAIL: " + message); checks++; Console.WriteLine("PASS: " + message); }
async Task Expected(int status, Func<Task> action)
{
    try { await action(); } catch (ApiException e) when (e.StatusCode == status) { Check(true, "Expected " + status); return; }
    throw new Exception("Expected HTTP " + status);
}
ClaimsPrincipal Actor(int id, string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(ClaimTypes.Role,role)],"Test"));
async Task<T> Run<T>(Func<ITreatmentWorkflowService,Task<T>> action)
{
    await using var db = new ApplicationDbContext(options);
    return await action(new TreatmentWorkflowService(db,new PatientService(db),configuration));
}
async Task<T> Exam<T>(Func<IExaminationService,Task<T>> action)
{
    await using var db = new ApplicationDbContext(options);
    return await action(new ExaminationService(db,new PatientService(db)));
}
var suffix = Guid.NewGuid().ToString("N")[..12];
int patientId, doctorId, performerId, doctorUserId, performerUserId, receptionistId, serviceId;
await using (var db = new ApplicationDbContext(options))
{
    Check(await db.Database.CanConnectAsync(ct), "Dedicated V2 SQL database reachable");
    var doctorRole = await db.Roles.SingleAsync(r => r.RoleCode == RoleConstants.Doctor,ct);
    var receptionRole = await db.Roles.SingleAsync(r => r.RoleCode == RoleConstants.ReceptionStaff,ct);
    User NewUser(string name, Role role) => new() { Username=name+suffix,FullName=name,PasswordHash="TEST_ONLY",IsActive=true,UserRoles=[new UserRole {Role=role}] };
    var user1=NewUser("prescriber",doctorRole); var user2=NewUser("performer",doctorRole); var reception=NewUser("reception",receptionRole);
    db.Users.Add(reception);
    var doctor=new Doctor {User=user1,FullName="Prescriber "+suffix,IsActive=true};
    var performer=new Doctor {User=user2,FullName="Performer "+suffix,IsActive=true};
    db.Doctors.AddRange(doctor,performer);
    var patient=new Patient {FullName="Treatment test "+suffix,AllergyNote="Test allergy"};
    var service=new Service {ServiceCode="T_"+suffix,ServiceName="Test service",Unit="lần",Price=100000,IsActive=true};
    db.Patients.Add(patient); db.Services.Add(service); await db.SaveChangesAsync(ct);
    (patientId,doctorId,performerId,doctorUserId,performerUserId,receptionistId,serviceId)=
        (patient.PatientId,doctor.DoctorId,performer.DoctorId,user1.UserId,user2.UserId,reception.UserId,service.ServiceId);
}
var receptionActor=Actor(receptionistId,RoleConstants.ReceptionStaff);
var prescribingActor=Actor(doctorUserId,RoleConstants.Doctor);
var performingActor=Actor(performerUserId,RoleConstants.Doctor);
async Task<int> NewPlan()
{
    var registration=await Exam(s=>s.CreateAsync(receptionActor,new() {PatientId=patientId,DoctorId=doctorId,Symptoms="Treatment test"},ct));
    await Exam(s=>s.StartAsync(prescribingActor,registration.RegistrationId,ct));
    var plan=await Exam(s=>s.CreatePlanAsync(prescribingActor,registration.RegistrationId,ct));
    plan=await Exam(s=>s.SavePlanAsync(prescribingActor,registration.RegistrationId,plan.PlanId,new() {
        RowVersion=plan.RowVersion,PlanName="Treatment "+suffix,TreatmentType="COURSE",PlannedSessionCount=2,
        Items=[new() {ServiceId=serviceId,PlannedQuantity=2}] },ct));
    await Exam(s=>s.ConfirmPlanAsync(prescribingActor,registration.RegistrationId,plan.PlanId,new() {RowVersion=plan.RowVersion},ct));
    return plan.PlanId;
}
async Task Pay(int planId)
{
    await using var db=new ApplicationDbContext(options);
    var plan=await db.TreatmentPlans.Include(p=>p.TreatmentPlanDetails).SingleAsync(p=>p.PlanId==planId,ct);
    var invoice=new Invoice {RecordId=plan.RecordId,CashierId=receptionistId,TotalAmount=200000,FinalAmount=200000,
        PaymentMethod="CASH",PaymentStatus="PAID",IssuedDate=DateTime.UtcNow};
    invoice.InvoiceDetails=plan.TreatmentPlanDetails.Select(d=>new InvoiceDetail {ItemType="SERVICE",PlanDetailId=d.PlanDetailId,
        ItemName=d.ServiceNameSnapshot,Quantity=d.PlannedQuantity,UnitPrice=d.UnitPrice}).ToList();
    db.Invoices.Add(invoice); await db.SaveChangesAsync(ct);
}
async Task<SessionOverview> Request(int planId)
{
    var plan=await Run(s=>s.PlanAsync(receptionActor,planId,ct));
    return await Run(s=>s.RequestAsync(receptionActor,planId,new() {RowVersion=plan.Plan.RowVersion,AssignedDoctorId=performerId,
        ScheduledDate=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))},ct));
}
var planId=await NewPlan();
await Expected(409,async()=>{await Request(planId);}); // no invoice
await Pay(planId);
var initial=await Run(s=>s.PlanAsync(receptionActor,planId,ct));
var request=new RequestTreatment {RowVersion=initial.Plan.RowVersion,AssignedDoctorId=performerId,ScheduledDate=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))};
async Task<int> AttemptRequest() { try { await Run(s=>s.RequestAsync(receptionActor,planId,request,ct)); return 200; } catch(ApiException e) { return e.StatusCode; } }
var attempts=await Task.WhenAll(AttemptRequest(),AttemptRequest());
Check(attempts.Count(s=>s==200)==1 && attempts.Count(s=>s==409)==1,"Concurrent requests create one session");
var list=await Run(s=>s.SessionsAsync(performingActor,new() {PlanId=planId},true,ct));
var first=await Run(s=>s.SessionAsync(performingActor,list.Items.Single().SessionId,ct));
await Expected(403,async()=>{await Run(s=>s.StartAsync(prescribingActor,first.Session.SessionId,new() {RowVersion=first.Session.RowVersion},ct));});
first=await Run(s=>s.StartAsync(performingActor,first.Session.SessionId,new() {RowVersion=first.Session.RowVersion},ct));
await Expected(409,async()=>{await Run(s=>s.CancelAsync(receptionActor,first.Session.SessionId,new() {RowVersion=first.Session.RowVersion,Reason="Too late"},ct));});
var detailId=first.TreatmentPlan.Services.Single().PlanDetailId;
CompleteTreatment Completion(SessionOverview value,int detail,int quantity) => new() {RowVersion=value.Session.RowVersion,ResultNote="Improved",Items=[new() {PlanDetailId=detail,Quantity=quantity}]};
var anotherPlan=await NewPlan(); var foreign=await Run(s=>s.PlanAsync(receptionActor,anotherPlan,ct));
await Expected(400,async()=>{await Run(s=>s.CompleteAsync(performingActor,first.Session.SessionId,Completion(first,foreign.Services.Single().PlanDetailId,1),ct));});
await Expected(409,async()=>{await Run(s=>s.CompleteAsync(performingActor,first.Session.SessionId,Completion(first,detailId,3),ct));});
await Expected(409,async()=>{await Run(s=>s.CompleteAsync(performingActor,first.Session.SessionId,Completion(first,detailId,2),ct));}); // consumes all too early
var completion=Completion(first,detailId,1);
var completed=await Task.WhenAll(Run(s=>s.CompleteAsync(performingActor,first.Session.SessionId,completion,ct)),Run(s=>s.CompleteAsync(performingActor,first.Session.SessionId,completion,ct)));
Check(completed.All(c=>c.Usage.Count==1 && c.TreatmentPlan.Plan.CompletedSessions==1 && c.TreatmentPlan.Plan.Status=="IN_PROGRESS"),"Concurrent identical complete is idempotent and starts plan progress");
await Expected(409,async()=>{await Run(s=>s.CompleteAsync(performingActor,first.Session.SessionId,Completion(first,detailId,2),ct));});
var next=await Request(planId);
var cancelled=await Run(s=>s.CancelAsync(receptionActor,next.Session.SessionId,new() {RowVersion=next.Session.RowVersion,Reason="Patient postponed"},ct));
Check(cancelled.Session.Status=="CANCELLED" && cancelled.TreatmentPlan.RemainingSessions==1,"Cancel preserves remaining entitlement");
next=await Request(planId);
Check(next.Session.SessionNumber>cancelled.Session.SessionNumber,"New attempt number is monotonic after cancellation");
Check(next.Session.SessionNumber>next.TreatmentPlan.Plan.PlannedSessionCount,"Attempt ordinal is distinct from prescribed capacity");
next=await Run(s=>s.StartAsync(performingActor,next.Session.SessionId,new() {RowVersion=next.Session.RowVersion},ct));
var final=await Run(s=>s.CompleteAsync(performingActor,next.Session.SessionId,Completion(next,detailId,1),ct));
Check(final.TreatmentPlan.Plan.Status=="COMPLETED" && final.TreatmentPlan.Plan.CompletedSessions==2 && final.TreatmentPlan.Services.Single().RemainingQuantity==0,"Final completion updates sessions and service progress");
await Expected(409,async()=>{await Request(planId);});
await Expected(409,async()=>{await Run(s=>s.CancelPlanAsync(prescribingActor,planId,new() {RowVersion=final.TreatmentPlan.Plan.RowVersion,Reason="Disallowed"},ct));});
await using(var db=new ApplicationDbContext(options))
{
    Check(await db.UsageHistories.CountAsync(u=>u.Session.PlanId==planId,ct)==2,"Exactly two usage rows persisted despite retries");
    Check(await db.MedicalRecords.CountAsync(r=>r.PatientId==patientId,ct)==2,"Treatment sessions create no new medical records (only two test examinations)");
}
Console.WriteLine($"{checks} SQL service checks passed. Fixtures suffix: {suffix}. Run HTTP/JWT checks separately.");
return 0;
