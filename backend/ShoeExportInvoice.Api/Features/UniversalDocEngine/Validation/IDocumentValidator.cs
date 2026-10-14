using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
public interface IDocumentValidator
{
    ValidationResult ValidateConfiguration(DocumentWorkflowProfile profile);
    ValidationResult Validate(CanonicalDocument document, DocumentWorkflowProfile profile);
}
