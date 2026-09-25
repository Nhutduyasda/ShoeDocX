import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Modal, Space, message } from 'antd';
import { ArrowLeftOutlined, ArrowRightOutlined } from '@ant-design/icons';
import { ocrApi } from '../api/ocrApi';
import type {
  CreateShipmentItem,
  MasterDataFolder,
  OcrDetectedDocument,
  ProductMaster,
} from '../types';
import { normalizeProcessType } from '../types';
import { SplitMatrixModal } from '../features/shipment-dispatch/components/SplitMatrixModal';
import type {
  ConsolidatedDispatchSource,
  DispatchSourceDocument,
  MergeShipmentPreviewResponse,
  OcrDispatchSourcePayload,
} from '../features/shipment-dispatch/types/shipmentDispatch';
import {
  parseDownloadError,
  shipmentDispatchApi,
  triggerDownload,
} from '../features/shipment-dispatch/api/shipmentDispatchApi';
import {
  getReconciliationStatus,
  reEnrichOcrDocumentsForPartner,
} from '../utils/ocrReconciliation';
import { OcrWorkflowStepper, type OcrWorkflowStep, STEP_KEYS } from './ocr/OcrWorkflowStepper';
import { OcrPartnerSelector } from './ocr/OcrPartnerSelector';
import { ManualOcrConfirmationModal } from './ocr/ManualOcrConfirmationModal';
import { OcrReviewStep } from './ocr/OcrReviewStep';
import { OcrDispatchStep } from './ocr/OcrDispatchStep';
import { OcrResultStep } from './ocr/OcrResultStep';
import { OcrExportStep } from './ocr/OcrExportStep';
import { OcrDoneStep } from './ocr/OcrDoneStep';

interface Props {
  visible: boolean;
  onClose: () => void;
  products: ProductMaster[];
  selectedPartnerId?: number | null;
  partnerFolders?: MasterDataFolder[];
  onPartnerChange?: (partnerId: number) => void;
  onApply: (items: CreateShipmentItem[], mode: 'replace' | 'append') => void;
  onDispatchExported?: () => void;
  templateId?: number | null;
  poSuffix?: string;
  invoiceDate?: string;
}

export const OcrUploadModal: React.FC<Props> = ({
  visible,
  onClose,
  products,
  selectedPartnerId,
  partnerFolders = [],
  onPartnerChange,
  onApply,
  onDispatchExported,
  templateId,
  poSuffix,
  invoiceDate,
}) => {
  // WORKFLOW STATE
  const [step, setStep] = useState<OcrWorkflowStep>('review');
  const [maxReachedStep, setMaxReachedStep] = useState<number>(0);

  // PARTNER IN-MODAL STATE
  const [inModalPartnerId, setInModalPartnerId] = useState<number | null>(selectedPartnerId ?? null);

  useEffect(() => {
    if (selectedPartnerId !== undefined) {
      setInModalPartnerId(selectedPartnerId);
    }
  }, [selectedPartnerId]);

  // OCR DATA STATE
  const [file, setFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [documents, setDocuments] = useState<OcrDetectedDocument[]>([]);
  const [activeDocumentId, setActiveDocumentId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // DISPATCH STATE
  const [selectedDocumentIds, setSelectedDocumentIds] = useState<string[]>([]);
  const [processedDocumentIds, setProcessedDocumentIds] = useState<Set<string>>(new Set());

  // VERSIONING & STALE CONTROL
  const [sourceVersion, setSourceVersion] = useState<number>(1);
  const [previewVersion, setPreviewVersion] = useState<number | undefined>(undefined);

  // DOWNSTREAM RESULTS
  const [mergePreview, setMergePreview] = useState<MergeShipmentPreviewResponse | null>(null);
  const [isPreviewingMerge, setIsPreviewingMerge] = useState(false);
  const [isExportingMerge, setIsExportingMerge] = useState(false);
  const [isExportingSeparate, setIsExportingSeparate] = useState(false);
  const [exportError, setExportError] = useState<{ message: string; traceId?: string } | null>(null);
  const [exportSuccess, setExportSuccess] = useState<{ fileName: string; count: number } | null>(null);

  // MANUAL CONFIRMATION MODAL
  const [confirmModalOpen, setConfirmModalOpen] = useState(false);
  const [documentToConfirm, setDocumentToConfirm] = useState<OcrDetectedDocument | null>(null);
  const [isConfirming, setIsConfirming] = useState(false);

  // SPLIT MODALS
  const [splitSource, setSplitSource] = useState<DispatchSourceDocument | null>(null);
  const [consolidatedSplitSource, setConsolidatedSplitSource] = useState<ConsolidatedDispatchSource | null>(null);

  // REVOKE BLOB URL
  useEffect(() => () => {
    if (previewUrl?.startsWith('blob:')) URL.revokeObjectURL(previewUrl);
  }, [previewUrl]);

  // RESET ON MODAL CLOSE
  const handleModalClose = () => {
    onClose();
  };

  // CHANGE STEP WITH PROGRESSION TRACKING
  const goToStep = (nextStep: OcrWorkflowStep) => {
    const nextIdx = STEP_KEYS.indexOf(nextStep);
    setMaxReachedStep((prev) => Math.max(prev, nextIdx));
    setStep(nextStep);
  };

  // QUICK PARTNER SWITCH (ZERO AI TOKEN)
  const handlePartnerChange = useCallback(
    (newPartnerId: number) => {
      setInModalPartnerId(newPartnerId);
      onPartnerChange?.(newPartnerId);

      // Re-enrich documents with new partner's product master without OCR
      setDocuments((prevDocs) => {
        const enriched = reEnrichOcrDocumentsForPartner(prevDocs, newPartnerId, products);
        return enriched;
      });

      // Increment source version to mark downstream preview stale
      setSourceVersion((v) => v + 1);

      const folderName = partnerFolders.find((f) => f.id === newPartnerId)?.name || 'đối tác mới';
      message.success(`Đã cập nhật danh mục theo ${folderName} (không gọi lại OCR).`);
    },
    [products, partnerFolders, onPartnerChange]
  );

  // OCR EXTRACTION (ONLY CALLED ON IMAGE UPLOAD OR RESCAN)
  const processImage = useCallback(
    async (imageFile: File | Blob) => {
      setLoading(true);
      setExportError(null);
      try {
        const response = await ocrApi.extractFromImage(imageFile);
        const initialEnriched = reEnrichOcrDocumentsForPartner(
          response.documents,
          inModalPartnerId,
          products
        );
        setDocuments(initialEnriched);
        setActiveDocumentId(initialEnriched[0]?.documentId ?? null);
        setSelectedDocumentIds(initialEnriched.map((d) => d.documentId));
        setSourceVersion((v) => v + 1);
        setMergePreview(null);
        setPreviewVersion(undefined);
        setMaxReachedStep(0);
        setStep('review');
        message.success(`Đã bóc tách thành công ${initialEnriched.length} đợt hàng từ ảnh.`);
      } catch (e: unknown) {
        const text =
          (e as { response?: { data?: { message?: string } } })?.response?.data?.message ||
          'Không thể nhận diện ảnh phiếu kho.';
        message.error(text);
      } finally {
        setLoading(false);
      }
    },
    [inModalPartnerId, products]
  );

  const selectFile = (selected: File) => {
    if (!selected.type.startsWith('image/')) {
      return message.error('Vui lòng chọn file ảnh hợp lệ.');
    }
    if (previewUrl?.startsWith('blob:')) URL.revokeObjectURL(previewUrl);
    setFile(selected);
    setPreviewUrl(URL.createObjectURL(selected));
    setDocuments([]);
    processImage(selected);
  };

  // CLIPBOARD PASTE
  useEffect(() => {
    if (!visible) return;
    const pasteHandler = (e: ClipboardEvent) => {
      const blob = Array.from(e.clipboardData?.items || [])
        .find((i) => i.type.startsWith('image/'))
        ?.getAsFile();
      if (blob) selectFile(blob);
    };
    window.addEventListener('paste', pasteHandler);
    return () => window.removeEventListener('paste', pasteHandler);
  });

  // UPDATE DOCUMENT
  const handleUpdateDocument = (
    id: string,
    updater: (d: OcrDetectedDocument) => OcrDetectedDocument
  ) => {
    setDocuments((prev) => prev.map((d) => (d.documentId === id ? updater(d) : d)));
    setSourceVersion((v) => v + 1);
  };

  // MANUAL CONFIRMATION FLOW
  const handleOpenManualConfirm = (doc: OcrDetectedDocument) => {
    setDocumentToConfirm(doc);
    setConfirmModalOpen(true);
  };

  const handleConfirmMismatch = async (reason: string) => {
    if (!documentToConfirm) return;
    setIsConfirming(true);
    try {
      await ocrApi.confirmMismatch({
        documentId: documentToConfirm.documentId,
        documentTitle: documentToConfirm.title,
        reportedTotal: documentToConfirm.reportedTotal,
        calculatedTotal: documentToConfirm.calculatedTotal,
        contractFolderId: inModalPartnerId,
        reason,
      });

      setDocuments((prev) =>
        prev.map((d) =>
          d.documentId === documentToConfirm.documentId
            ? { ...d, isManuallyConfirmed: true, confirmationReason: reason }
            : d
        )
      );
      setSourceVersion((v) => v + 1);
      message.success(`Đã xác nhận đối soát thủ công cho “${documentToConfirm.title}”.`);
      setConfirmModalOpen(false);
      setDocumentToConfirm(null);
    } catch {
      message.error('Không thể lưu xác nhận đối soát vào hệ thống.');
    } finally {
      setIsConfirming(false);
    }
  };

  // SELECTION
  const handleToggleSelectDocument = (id: string, checked: boolean) => {
    setSelectedDocumentIds((current) =>
      checked ? [...new Set([...current, id])] : current.filter((val) => val !== id)
    );
    setSourceVersion((v) => v + 1);
  };

  const handleSelectAll = () => {
    const availableIds = documents
      .filter((d) => !processedDocumentIds.has(d.documentId) && getReconciliationStatus(d) !== 'MISMATCH')
      .map((d) => d.documentId);
    setSelectedDocumentIds(availableIds);
    setSourceVersion((v) => v + 1);
  };

  const handleDeselectAll = () => {
    setSelectedDocumentIds([]);
    setSourceVersion((v) => v + 1);
  };

  // FILTERED SELECTED DOCUMENTS
  const selectedDocuments = useMemo(
    () =>
      documents.filter(
        (d) => selectedDocumentIds.includes(d.documentId) && !processedDocumentIds.has(d.documentId)
      ),
    [documents, selectedDocumentIds, processedDocumentIds]
  );

  const localSelectedTotal = useMemo(
    () => selectedDocuments.reduce((sum, d) => sum + d.calculatedTotal, 0),
    [selectedDocuments]
  );

  // BUILD DISPATCH PAYLOADS
  const toDispatchSource = (doc: OcrDetectedDocument): DispatchSourceDocument => ({
    sourceType: 'ocr-document',
    documentId: doc.documentId,
    title: doc.title,
    items: doc.items.map((i) => ({
      styleCode: i.styleCode,
      quantity: i.quantity,
      processType: normalizeProcessType(i.processType),
      unitPriceCMT: i.unitPriceCMT,
      unitPriceDAP: i.unitPriceDAP,
      unit: i.unit,
      pairPerCarton: i.pairPerCarton,
      description: i.description,
    })),
    calculatedTotal: doc.calculatedTotal,
    reportedTotal: doc.reportedTotal,
    sourceFileName: file?.name,
    isManuallyConfirmed: doc.isManuallyConfirmed,
    confirmationReason: doc.confirmationReason,
  });

  const sourcePayloads = (): OcrDispatchSourcePayload[] =>
    selectedDocuments.map((doc) => ({
      documentId: doc.documentId,
      title: doc.title,
      sourceFileName: file?.name,
      reportedTotal: doc.reportedTotal,
      calculatedTotal: doc.calculatedTotal,
      isManuallyConfirmed: doc.isManuallyConfirmed,
      confirmationReason: doc.confirmationReason,
      items: toDispatchSource(doc).items,
    }));

  const mergeRequest = () => ({
    sourceDocuments: sourcePayloads(),
    contractFolderId: inModalPartnerId ?? undefined,
    templateId: templateId ?? undefined,
    poSuffix,
    invoiceDate: invoiceDate || new Date().toISOString().slice(0, 10),
  });

  // MERGE PREVIEW
  const handlePreviewMerge = async () => {
    if (selectedDocuments.length < 2) return;
    setIsPreviewingMerge(true);
    setExportError(null);
    try {
      const preview = await shipmentDispatchApi.previewMerge(mergeRequest());
      setMergePreview(preview);
      setPreviewVersion(sourceVersion);
      goToStep('result');
    } catch (error: unknown) {
      const parsed = await parseDownloadError(error);
      setExportError({ message: parsed.message, traceId: parsed.traceId });
      message.error(parsed.message || 'Không thể xem trước kết quả gom.');
    } finally {
      setIsPreviewingMerge(false);
    }
  };

  // EXPORT MERGE
  const handleExportMerge = async () => {
    if (!mergePreview?.isExportable || isExportingMerge) return;
    setIsExportingMerge(true);
    setExportError(null);
    try {
      const result = await shipmentDispatchApi.exportMerge(mergeRequest());
      triggerDownload(result);
      setProcessedDocumentIds((current) => new Set([...current, ...selectedDocuments.map((d) => d.documentId)]));
      setExportSuccess({
        fileName: result.fileName,
        count: mergePreview.generatedDocumentCount,
      });
      onDispatchExported?.();
      goToStep('done');
    } catch (error: unknown) {
      const parsed = await parseDownloadError(error);
      setExportError({
        message: [parsed.message, ...parsed.validationErrors].filter(Boolean).join(' '),
        traceId: parsed.traceId,
      });
    } finally {
      setIsExportingMerge(false);
    }
  };

  // EXPORT SEPARATE
  const handleExportSeparate = async () => {
    if (!selectedDocuments.length) return;
    setIsExportingSeparate(true);
    try {
      const batches = selectedDocuments.map((doc) => ({
        batchId: doc.documentId,
        title: doc.title,
        items: toDispatchSource(doc).items,
      }));
      const blob = await ocrApi.batchExportZip({
        contractFolderId: inModalPartnerId ?? undefined,
        poSuffix,
        invoiceDate,
        batches,
      });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `OCR-Separate-${invoiceDate || new Date().toISOString().slice(0, 10)}.zip`;
      anchor.click();
      URL.revokeObjectURL(url);
      setProcessedDocumentIds((current) => new Set([...current, ...selectedDocuments.map((d) => d.documentId)]));
      setExportSuccess({
        fileName: anchor.download,
        count: batches.length,
      });
      onDispatchExported?.();
      goToStep('done');
    } catch {
      message.error('Không thể xuất riêng các đợt đã chọn.');
    } finally {
      setIsExportingSeparate(false);
    }
  };

  // APPLY SINGLE INVOICE
  const handleApplySingle = (doc: OcrDetectedDocument) => {
    const valid = doc.items.filter((i) => i.quantity > 0 && i.styleCode.trim());
    if (!valid.length) return message.warning('Đợt này không có mặt hàng hợp lệ.');
    onApply(
      valid.map((i) => ({
        styleCode: i.styleCode,
        quantity: i.quantity,
        processType: normalizeProcessType(i.processType),
        unitPriceCMT: i.unitPriceCMT,
        unitPriceDAP: i.unitPriceDAP,
        unit: i.unit,
        pairPerCarton: i.pairPerCarton,
        description: i.description,
      })),
      'replace'
    );
    message.success(`Đã áp dụng đợt “${doc.title || 'Không tiêu đề'}” vào hóa đơn.`);
    onClose();
  };

  // CONSOLIDATED SPLIT SOURCE
  const consolidatedSource = (): ConsolidatedDispatchSource | null =>
    mergePreview
      ? {
          sourceType: 'merged-ocr-documents',
          sourceDocumentIds: selectedDocuments.map((d) => d.documentId),
          sourceTitles: selectedDocuments.map((d) => d.title),
          sourceDocuments: sourcePayloads(),
          title: selectedDocuments.map((d) => d.title).join(' + '),
          items: mergePreview.mergedItems,
          totalQuantity: mergePreview.totalQuantity,
          totalCartons: mergePreview.totalCartons,
        }
      : null;

  // STALE DETECTION
  const isStale = previewVersion !== undefined && sourceVersion > previewVersion;

  // STEP VALIDATION
  const canProceedToDispatch = documents.length > 0 && selectedDocuments.length > 0;
  const canProceedToResult = Boolean(mergePreview && !isStale);
  const canProceedToExport = Boolean(mergePreview?.isExportable && !isStale && inModalPartnerId);

  // CURRENT PARTNER OBJECT
  const currentPartner = partnerFolders.find((f) => f.id === inModalPartnerId) || null;

  return (
    <Modal
      title={
        <div className="flex flex-wrap justify-between items-center pr-8 gap-3">
          <div className="font-semibold text-slate-800 text-base">
            OCR Phiếu kho &mdash; Quy trình đối soát và xuất chứng từ
          </div>
          <OcrPartnerSelector
            partners={partnerFolders}
            selectedPartnerId={inModalPartnerId}
            onPartnerChange={handlePartnerChange}
          />
        </div>
      }
      open={visible}
      onCancel={handleModalClose}
      width="96vw"
      style={{ maxWidth: 1600, top: 15 }}
      bodyStyle={{ padding: 0 }}
      footer={
        <div className="flex justify-between items-center px-4 py-2 bg-slate-50 border-t border-slate-200">
          <div>
            {step === 'review' && (
              <Button onClick={handleModalClose}>Đóng</Button>
            )}
            {step === 'dispatch' && (
              <Button icon={<ArrowLeftOutlined />} onClick={() => goToStep('review')}>
                Quay lại đối soát (Bước 1)
              </Button>
            )}
            {step === 'result' && (
              <Button icon={<ArrowLeftOutlined />} onClick={() => goToStep('dispatch')}>
                Quay lại điều phối (Bước 2)
              </Button>
            )}
            {step === 'export' && (
              <Button icon={<ArrowLeftOutlined />} onClick={() => goToStep('result')}>
                Quay lại kết quả (Bước 3)
              </Button>
            )}
            {step === 'done' && (
              <Button onClick={handleModalClose}>Đóng</Button>
            )}
          </div>

          <Space>
            {step === 'review' && (
              <Button
                type="primary"
                disabled={!canProceedToDispatch}
                onClick={() => goToStep('dispatch')}
              >
                Tiếp tục điều phối <ArrowRightOutlined />
              </Button>
            )}
            {step === 'result' && (
              <Button
                type="primary"
                disabled={!mergePreview?.isExportable || isStale}
                onClick={() => goToStep('export')}
              >
                Tiếp tục kiểm tra xuất <ArrowRightOutlined />
              </Button>
            )}
          </Space>
        </div>
      }
    >
      {/* GUIDED WORKFLOW STEPPER */}
      <OcrWorkflowStepper
        currentStep={step}
        maxReachedStep={maxReachedStep}
        onStepChange={(s) => setStep(s)}
        canProceedToDispatch={canProceedToDispatch}
        canProceedToResult={canProceedToResult}
        canProceedToExport={canProceedToExport}
        isDone={step === 'done'}
      />

      {/* STEP CONTENT */}
      <div className="min-h-[600px]">
        {step === 'review' && (
          <OcrReviewStep
            file={file}
            previewUrl={previewUrl}
            documents={documents}
            activeDocumentId={activeDocumentId}
            selectedDocumentIds={selectedDocumentIds}
            loading={loading}
            products={products}
            selectedPartnerId={inModalPartnerId}
            onSelectFile={selectFile}
            onRescanImage={() => file && processImage(file)}
            onSetActiveDocumentId={setActiveDocumentId}
            onToggleSelectDocument={handleToggleSelectDocument}
            onUpdateDocument={handleUpdateDocument}
            onRequestManualConfirm={handleOpenManualConfirm}
          />
        )}

        {step === 'dispatch' && (
          <OcrDispatchStep
            documents={documents}
            selectedDocumentIds={selectedDocumentIds}
            processedDocumentIds={processedDocumentIds}
            onToggleSelectDocument={handleToggleSelectDocument}
            onSelectAll={handleSelectAll}
            onDeselectAll={handleDeselectAll}
            onPreviewMerge={handlePreviewMerge}
            onExportSeparate={handleExportSeparate}
            onOpenSplitSingle={(doc) => setSplitSource(toDispatchSource(doc))}
            onApplySingleInv={handleApplySingle}
            isPreviewingMerge={isPreviewingMerge}
            isExportingSeparate={isExportingSeparate}
          />
        )}

        {step === 'result' && (
          <OcrResultStep
            mergePreview={mergePreview}
            isStale={isStale}
            selectedDocCount={selectedDocuments.length}
            localSelectedTotal={localSelectedTotal}
            onRecalculateMerge={handlePreviewMerge}
            onOpenConsolidatedSplit={() => {
              const source = consolidatedSource();
              if (source) setConsolidatedSplitSource(source);
            }}
            isLoading={isPreviewingMerge}
          />
        )}

        {step === 'export' && (
          <OcrExportStep
            partners={partnerFolders}
            selectedPartnerId={inModalPartnerId}
            onPartnerChange={handlePartnerChange}
            selectedDocuments={selectedDocuments}
            mergePreview={mergePreview}
            isStale={isStale}
            isExporting={isExportingMerge}
            onExport={handleExportMerge}
            exportError={exportError}
          />
        )}

        {step === 'done' && (
          <OcrDoneStep
            selectedDocuments={selectedDocuments}
            partner={currentPartner}
            exportedFileName={exportSuccess?.fileName}
            documentCount={exportSuccess?.count}
            totalQuantity={localSelectedTotal}
            onClose={handleModalClose}
            onScanNewImage={() => {
              setDocuments([]);
              setPreviewUrl(null);
              setFile(null);
              setStep('review');
              setMaxReachedStep(0);
            }}
          />
        )}
      </div>

      {/* MANUAL CONFIRMATION DIALOG */}
      <ManualOcrConfirmationModal
        open={confirmModalOpen}
        document={documentToConfirm}
        onCancel={() => {
          setConfirmModalOpen(false);
          setDocumentToConfirm(null);
        }}
        onConfirm={handleConfirmMismatch}
        loading={isConfirming}
      />

      {/* SPLIT MATRIX MODALS */}
      <SplitMatrixModal
        open={Boolean(splitSource)}
        sourceDocument={splitSource}
        contractFolderId={inModalPartnerId ?? undefined}
        templateId={templateId}
        poSuffix={poSuffix}
        invoiceDate={invoiceDate || new Date().toISOString().slice(0, 10)}
        onClose={() => setSplitSource(null)}
        onExported={() => {
          if (splitSource) {
            setProcessedDocumentIds((current) => new Set(current).add(splitSource.documentId));
          }
          onDispatchExported?.();
          goToStep('done');
        }}
      />

      <SplitMatrixModal
        open={Boolean(consolidatedSplitSource)}
        consolidatedSource={consolidatedSplitSource}
        contractFolderId={inModalPartnerId ?? undefined}
        templateId={templateId}
        poSuffix={poSuffix}
        invoiceDate={invoiceDate || new Date().toISOString().slice(0, 10)}
        onClose={() => setConsolidatedSplitSource(null)}
        onExported={() => {
          if (consolidatedSplitSource) {
            setProcessedDocumentIds(
              (current) => new Set([...current, ...consolidatedSplitSource.sourceDocumentIds])
            );
          }
          onDispatchExported?.();
          goToStep('done');
        }}
      />
    </Modal>
  );
};
