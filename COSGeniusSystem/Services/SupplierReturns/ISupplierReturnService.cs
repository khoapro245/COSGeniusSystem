using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface ISupplierReturnService
{
    Task<List<SupplierReturnDto>> GetSupplierReturnsAsync(CancellationToken ct = default);
    Task<SupplierReturnDto?> GetSupplierReturnByIdOrCodeAsync(string idOrCode, CancellationToken ct = default);
    Task<SupplierReturnDto> CreateSupplierReturnAsync(int userId, CreateSupplierReturnRequest request, CancellationToken ct = default);
    Task<SupplierReturnDto> ApproveSupplierReturnAsync(int userId, string idOrCode, ApproveSupplierReturnRequest request, CancellationToken ct = default);
    Task<SupplierReturnDto> RejectSupplierReturnAsync(int userId, string idOrCode, RejectSupplierReturnRequest request, CancellationToken ct = default);
    Task<SupplierReturnDto> HandoverSupplierReturnAsync(int userId, string idOrCode, HandoverSupplierReturnRequest request, CancellationToken ct = default);
}

