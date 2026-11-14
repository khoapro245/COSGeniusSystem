using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace COSGeniusSystem.UnitTests;
public class PatientPhoneTests {
 private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
 [Theory]
 [InlineData("0912345678")]
 [InlineData("+84912345678")]
 [InlineData(" (091) 234-56.78 ")]
 [InlineData("+84 912 345 678")]
 public void NormalizeEquivalent(string value) => Assert.Equal("0912345678", PatientPhone.Validate(value));
 [Theory]
 [InlineData("abc")][InlineData("09123456789")][InlineData("+840912345678")][InlineData("091/2345678")]
 public void Invalid(string value) => Assert.Throws<BadRequestException>(() => PatientPhone.Validate(value));
 [Fact] public void OptionalBlank() => Assert.Null(PatientPhone.Validate("  "));
 [Fact] public async Task HistoricalFormatsAndSelfExclusion() {
  using var db=Db();
  db.Patients.AddRange(new Patient {FullName="A",Phone="+84 912-345-678"},new Patient {FullName="B",Phone="0912345678"},new Patient {FullName="C"});
  await db.SaveChangesAsync(); var service=new PatientService(db);
  Assert.Equal(2,(await service.CheckDuplicatePhoneAsync("0912345678",null,default)).Count);
  Assert.Single(await service.CheckDuplicatePhoneAsync("+84912345678",1,default));
  Assert.Empty(await service.CheckDuplicatePhoneAsync("",null,default));
  Assert.Empty(await service.CheckDuplicatePhoneAsync("0987654321",null,default));
 }
 [Fact] public async Task CreateRequiresConfirmationThenKeepsDistinctProfiles() {
  using var db=Db(); var service=new PatientService(db);
  await service.CreateAsync(new CreatePatientRequest { FullName="A", Phone="0912345678" },default);
  var request=new CreatePatientRequest {FullName="B",Phone="+84912345678"};
  await Assert.ThrowsAsync<DuplicatePatientPhoneException>(()=>service.CreateAsync(request,default));
  Assert.Equal(1,await db.Patients.CountAsync());
  request.ConfirmDuplicatePhone=true; await service.CreateAsync(request,default);
  Assert.Equal(2,await db.Patients.CountAsync());
  request.Phone="invalid";await Assert.ThrowsAsync<BadRequestException>(()=>service.CreateAsync(request,default));
 }
 [Fact] public async Task UpdateExcludesSelfAndWarnsForOtherPatient() {
  using var db=Db(); var service=new PatientService(db);
  var a=await service.CreateAsync(new CreatePatientRequest {FullName="A",Phone="0912345678"},default);
  var b=await service.CreateAsync(new CreatePatientRequest {FullName="B",Phone="0987654321"},default);
  await service.UpdateAsync(a.PatientId,new UpdatePatientRequest {FullName="A",Phone="+84912345678"},default);
  var request=new UpdatePatientRequest {FullName="B",Phone="0912345678"};
  await Assert.ThrowsAsync<DuplicatePatientPhoneException>(()=>service.UpdateAsync(b.PatientId,request,default));
  request.ConfirmDuplicatePhone=true;await service.UpdateAsync(b.PatientId,request,default);
  request.Email="invalid";await Assert.ThrowsAsync<BadRequestException>(()=>service.UpdateAsync(b.PatientId,request,default));
 }
}
