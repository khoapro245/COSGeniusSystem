using System.ComponentModel.DataAnnotations;
using System.Data;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace COSGeniusSystem.Controllers;
[COSGeniusSystem.Services.Herbalist.HerbalistConflict]
[ApiController]
[Route("api/v1/warehouse/storage-locations")]
[Authorize(Roles="ADMIN,INVENTORY_STAFF")]
public class HerbalistStorageController(ApplicationDbContext db):ControllerBase
{
 public record CreateLocation([property:Range(1,int.MaxValue)] int WarehouseId,[property:Required,StringLength(100,MinimumLength=1)] string Code);
 public record AssignLocation([property:Range(1,int.MaxValue)] int StorageLocationId);
 private IActionResult Result(int status,string message,object? data=null)=>StatusCode(status,new ApiResponseModel<object>(status,message,status<300,data));
 [HttpGet]
 public async Task<IActionResult> List([FromQuery,Range(1,int.MaxValue)] int warehouseId,CancellationToken ct)=>Result(200,"Thành công.",await db.WarehouseStorageLocations.AsNoTracking().Where(x=>x.WarehouseId==warehouseId).OrderBy(x=>x.Code).ToListAsync(ct));
 [HttpPost]
 public async Task<IActionResult> Create(CreateLocation input,CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(input.Code) || !await db.Warehouses.AnyAsync(x=>x.WarehouseId==input.WarehouseId && x.IsActive,ct))return Result(400,"Kho hoặc mã kệ/ngăn không hợp lệ.");
  var location=new WarehouseStorageLocation {WarehouseId=input.WarehouseId,Code=input.Code.Trim()};db.WarehouseStorageLocations.Add(location);
  try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){return Result(409,"Mã vị trí đã tồn tại trong kho.");}
  return Result(201,"Đã tạo vị trí.",new {location.Id,location.Code,location.WarehouseId});
 }
 [HttpPut("batches/{batchId:int}")]
 public async Task<IActionResult> Assign(int batchId,AssignLocation input,CancellationToken ct)
 {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  var batch=await db.Batches.SingleOrDefaultAsync(x=>x.BatchId==batchId,ct);
  if(batch==null)return Result(404,"Không tìm thấy lô.");
  if(!await db.WarehouseStorageLocations.AnyAsync(x=>x.Id==input.StorageLocationId && x.WarehouseId==batch.WarehouseId && x.IsActive,ct))return Result(400,"Vị trí phải thuộc cùng kho và đang hoạt động.");
  batch.StorageLocationId=input.StorageLocationId;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
  return Result(200,"Đã gán một vị trí cho lô.",new {batch.BatchId,batch.StorageLocationId});
 }
}
