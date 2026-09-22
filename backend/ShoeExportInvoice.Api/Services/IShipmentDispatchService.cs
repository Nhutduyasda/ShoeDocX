using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface IShipmentDispatchService
{
    Task<MergeShipmentPreviewResponseDto> PreviewMergeAsync(MergeShipmentRequestDto request, CancellationToken cancellationToken);
    Task<ValidateSplitResultDto> ValidateSplitAsync(ValidateSplitRequestDto request, CancellationToken cancellationToken);
    Task<ExportFileResult> ExportMergeAsync(MergeShipmentRequestDto request, CancellationToken cancellationToken);
    Task<ExportFileResult> ExportSplitZipAsync(SplitShipmentRequestDto request, CancellationToken cancellationToken);
}

public sealed class DispatchValidationException : InvalidOperationException
{
    public ValidateSplitResultDto Result { get; }
    public DispatchValidationException(ValidateSplitResultDto result) : base("Phân bổ hóa đơn không hợp lệ.") => Result = result;
}
