using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Controllers;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
namespace COSGeniusSystem.UnitTests;

internal static class InvoiceTestSupport
{
    internal static ClaimsPrincipal Actor(string role = "CASHIER", string id = "1") => new(new ClaimsIdentity(new[] {
        new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role)
    }, "test"));
    internal static InvoicesController Controller(IInvoiceService service, ClaimsPrincipal actor) => new(service) {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = actor } }
    };
    internal static InvoicePaymentRequest Payment() => new() {
        RowVersion = new byte[8], PaymentMethod = "Tiền mặt", ReceivedInFull = true
    };
    internal static bool Valid(object request) => Validator.TryValidateObject(request,
        new ValidationContext(request), new List<ValidationResult>(), true);
    // PaginatedResult source was not supplied. Inspect its typed collection without assuming Items/Data naming.
    internal static List<InvoiceListDto> Rows(object page) => Assert.Single(page.GetType().GetProperties()
        .Where(p => p.GetIndexParameters().Length == 0).Select(p => p.GetValue(page))
        .OfType<IEnumerable<InvoiceListDto>>()).ToList();

    internal sealed class Scope : IDisposable
    {
        internal ApplicationDbContext Db { get; }
        internal MedicalRecord Record { get; }
        internal InvoiceService Api { get; }
        private Scope()
        {
            Db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"InvoiceTests_{Guid.NewGuid():N}").Options);
            Db.SavingChanges += (_, _) => {
                Db.ChangeTracker.DetectChanges();
                foreach (var e in Db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
                    if (e.Metadata.FindProperty("RowVersion")?.ClrType == typeof(byte[])
                        && (e.Property("RowVersion").CurrentValue is not byte[] b || b.Length == 0))
                        e.Property("RowVersion").CurrentValue = new byte[8];
            };
            Record = SchemaFixture.Record();
            Record.RecordId = 1; Record.Patient.PatientId = 1; Record.PatientId = 1;
            Record.Patient.FullName = "Patient current"; Record.Patient.PatientCode = "BN001"; Record.Patient.Phone = "0912345678";
            Record.Doctor.DoctorId = 1; Record.Doctor.User!.UserId = 1; Record.Doctor.UserId = 1; Record.DoctorId = 1;
            Record.Doctor.FullName = "Doctor current"; Record.Doctor.User.IsActive = true;
            Record.Registration.RegistrationId = 1; Record.RegistrationId = 1;
            Record.Registration.Patient = Record.Patient; Record.Registration.PatientId = 1;
            Record.Registration.Doctor = Record.Doctor; Record.Registration.DoctorId = 1;
            Record.Registration.RegisteredByNavigation = Record.Doctor.User; Record.Registration.RegisteredBy = 1;
            Record.Status = Record.Registration.Status = "COMPLETED";
            Record.CompletedAt = Record.Registration.CompletedAt = DateTime.UtcNow;
            Db.MedicalRecords.Add(Record); Api = new(Db);
        }
        internal static async Task<Scope> CreateAsync() {
            var s = new Scope(); await s.Db.SaveChangesAsync();
            Assert.Equal(1, (await s.Db.MedicalRecords.AsNoTracking().SingleAsync()).PatientId);
            return s;
        }
        internal async Task<Invoice> InvoiceAsync(int id = 1, string status = "UNPAID", DateTime? issued = null) {
            var invoice = new Invoice { InvoiceId = id, Record = Record, RecordId = 1,
                InvoiceCode = $"HD{id:D3}", IssuedDate = issued ?? DateTime.UtcNow,
                PaymentStatus = status, PaymentMethod = status == "PAID" ? "Tiền mặt" : "",
                TotalAmount = 100m, FinalAmount = 100m, RowVersion = new byte[8] };
            Db.Invoices.Add(invoice); await Db.SaveChangesAsync(); return invoice;
        }
        internal async Task<HerbalPrescription> PrescriptionAsync() {
            var rx = new HerbalPrescription { PrescriptionId = 1, Record = Record, RecordId = 1,
                CreatedByNavigation = Record.Doctor.User!, Status = "SENT", TotalDoses = 5,
                PrescriptionName = "Formula", UsageInstructions = "Use", RowVersion = new byte[8] };
            rx.HerbalPrescriptionDetails.Add(new HerbalPrescriptionDetail { HerbNameSnapshot = "Herb",
                DosagePerDose = 10m, Herb = new Herb { HerbId = 1, HerbCode = "H1", CommonName = "Herb",
                    Unit = "kg", StandardPrice = 200000m, IsActive = true } });
            Db.HerbalPrescriptions.Add(rx); await Db.SaveChangesAsync(); return rx;
        }
        internal async Task<TreatmentPlanDetail> PlanAsync(int id = 1, string type = "COURSE", int quantity = 3) {
            var plan = new TreatmentPlan { PlanId = id, Record = Record, RecordId = 1,
                Doctor = Record.Doctor, DoctorId = 1, PlanName = type, TreatmentType = type,
                PlannedSessionCount = type == "SINGLE" ? 1 : quantity,
                Status = "CONFIRMED", ConfirmedAt = DateTime.UtcNow, RowVersion = new byte[8] };
            var detail = new TreatmentPlanDetail { PlanDetailId = id, Plan = plan, PlanId = id,
                Service = new COSGeniusSystem.Models.Service { ServiceId = id, ServiceCode = $"DV{id}",
                    ServiceName = "Acupuncture", Unit = "session", Price = 100000m, IsActive = true },
                ServiceNameSnapshot = "Acupuncture", UnitSnapshot = "session", UnitPrice = 100000m, PlannedQuantity = quantity };
            Db.TreatmentPlanDetails.Add(detail); await Db.SaveChangesAsync(); return detail;
        }
        public void Dispose() => Db.Dispose();
    }
}
