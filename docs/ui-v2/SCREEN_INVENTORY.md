# Screen inventory — UI-0

Baseline: `329b684ae9daf71879e2198fff65faf073b11f05`. Source audit; authenticated visual coverage is not yet verified. Role authority remains `types/auth.ts` and server authorization.

## BomCalculatorPage

Source: `frontend/src/pages/BomCalculatorPage.tsx`. Roles: Admin, Kho.

Purpose / visible actions: BOM & Material Calculator; } loading=; } loading=

API and child dependencies: `@ant-design/icons`, `../api/bomApi`, `../components/BomResultTable`, `../components/ProductionOrderForm`, `../components/SizeMatrixInput`, `../types/bom`

Handlers (save/submit/export/navigation contracts retained): `handleCalculate`, `handleSave`, `onBomSelectionChange`, `onChange`, `onClear`, `onClick`, `onOrderNoChange`, `onSizesChange`

Validation / state evidence: `disabled=` (3), `loading` (6), `Table` (3). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## BomManagementPage

Source: `frontend/src/pages/BomManagementPage.tsx`. Roles: Admin, Kho.

Purpose / visible actions: } onClick=; } />; Định mức Kỹ thuật; } onClick=

API and child dependencies: `@ant-design/icons`, `antd/es/table`, `../api/bomApi`, `../api/productMasterApi`, `../components/BomFormModal`, `../types`, `../types/bom`

Handlers (save/submit/export/navigation contracts retained): `deleteDefinition`, `handleSave`, `loadData`, `onCancel`, `onClick`, `onConfirm`, `onSave`

Validation / state evidence: `loading` (3), `Modal` (8), `Table` (2). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## CustomsSettlementPage

Source: `frontend/src/pages/CustomsSettlementPage.tsx`. Roles: Admin, KeToan.

Purpose / visible actions: }
              onClick=; Quyết toán Hải quan & Thống kê Phân tích Kim ngạch; }
            onClick=; }
            onClick=; }
              disabled=; }
            onClick=; }
                      onClick=; }
                      onClick=; }
                      onClick=; }
                        onClick=; }
                      onClick=; }
                          onClick=; }
                          onClick=; }
                    onClick=; Biểu đồ Phân bổ Sản lượng & Doanh thu CMT theo 12 Tháng (Năm; Top 5 Mã Giày Xuất Khẩu Nhiều Nhất; Bảng Thống Kê Chi Tiết 12 Tháng; setDrillDownModalOpen(false)} className="text-xs">
            Đóng; }
                    onClick=; }
                    onClick=; setImportResultModalOpen(false)}>
            Đóng

API and child dependencies: `antd/es/table`, `@ant-design/icons`, `dayjs`, `../api/masterDataFolderApi`, `../types`, `../types`, `../components/SettlementTreePanel`, `../api/settlementApi`, `../types`

Handlers (save/submit/export/navigation contracts retained): `exportSettlementExcelById`, `handleCalculate`, `handleConfirmSave`, `handleDownloadSavedExcel`, `handleExportExcel`, `handleImportWarehouseExcel`, `handleItemChange`, `handleLoadSavedPeriod`, `handleOpenDrillDown`, `handleOpenSaveModal`, `handleParseAndMatchClipboard`, `handleSelectTreeNode`, `handleYearChange`, `loadAnalytics`, `loadSavedPeriods`, `onCancel`, `onChange`, `onClick`, `onClose`, `onConfirm`, `onMouseEnter`, `onMouseLeave`, `onOk`, `onRemove`, `onSelectNode`, `removeChild`, `saveForm`, `saveModalOpen`, `saveSettlement`, `saveStatus`, `validateFields`

Validation / state evidence: `rules=` (1), `required` (1), `disabled=` (8), `loading` (9), `error` (13), `Modal` (53), `Drawer` (11), `Table` (36). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## LoginPage

Source: `frontend/src/pages/LoginPage.tsx`. Roles: Unauthenticated.

Purpose / visible actions: ShoeDocX Enterprise; }
              loading=

API and child dependencies: `@ant-design/icons`, `../contexts/AuthContext`, `../types/auth`

Handlers (save/submit/export/navigation contracts retained): `handleSubmit`, `onFinish`

Validation / state evidence: `rules=` (2), `required` (2), `loading` (1), `onFinish` (1). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## MaterialManagementPage

Source: `frontend/src/pages/MaterialManagementPage.tsx`. Roles: Admin, Kho.

Purpose / visible actions: } onClick=; } aria-label=; Danh mục Vật tư; } onClick=

API and child dependencies: `@ant-design/icons`, `antd/es/table`, `../api/bomApi`, `../types/bom`

Handlers (save/submit/export/navigation contracts retained): `deleteMaterial`, `handleSave`, `loadMaterials`, `onCancel`, `onChange`, `onClick`, `onConfirm`, `onOk`, `validateFields`

Validation / state evidence: `rules=` (5), `required` (5), `loading` (3), `Modal` (7), `Table` (2). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## MaterialPlanningPage

Source: `frontend/src/pages/MaterialPlanningPage.tsx`. Roles: Admin, Kho.

Purpose / visible actions: } onClick=; } loading=; } loading=; }>Hủy; Kế hoạch & Xuất kho NPL; } onClick=

API and child dependencies: `@ant-design/icons`, `antd/es/table`, `../api/bomApi`, `../types/bom`

Handlers (save/submit/export/navigation contracts retained): `onClick`, `onConfirm`

Validation / state evidence: `required` (3), `loading` (7), `Table` (3). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## OverviewPage

Source: `frontend/src/pages/OverviewPage.tsx`. Roles: Admin, KeToan.

Purpose / visible actions: }
            onClick=; }
            onClick=; Tổng quan nghiệp vụ xuất khẩu; }
            onClick=; }
            onClick=; }
            onClick=; Chứng từ xuất hàng gần đây; onNavigate('history')}
            className="text-[#2563EB] hover:text-[#1D4ED8] text-xs p-0 flex items-center gap-1"
          >

API and child dependencies: `antd/es/table`, `@ant-design/icons`, `dayjs`, `../api/shipmentApi`, `../api/productMasterApi`, `../types`, `../layouts/AppLayout`

Handlers (save/submit/export/navigation contracts retained): `exportSavedShipmentExcel`, `handleDownloadHistorical`, `loadData`, `onClick`, `onNavigate`, `onOpenOrder`

Validation / state evidence: `loading` (3), `Table` (3). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## ProductMasterPage

Source: `frontend/src/pages/ProductMasterPage.tsx`. Roles: All authenticated departments.

Purpose / visible actions: }
              onClick=; }
              />; Danh mục Hàng hóa (Master Data); }
              onClick=; }
            onClick=; }
                onClick=; }
                loading=; }
              onClick=; }
                  onClick=

API and child dependencies: `@ant-design/icons`, `antd/es/table`, `../types`, `../api/productMasterApi`, `../api/masterDataFolderApi`, `../components/ProductModal`, `../components/ProductImportModal`, `../components/FolderTreePanel`, `../contexts/AuthContext`, `../types/auth`

Handlers (save/submit/export/navigation contracts retained): `deleteAll`, `deleteProduct`, `exportExcel`, `handleBulkUpdateUnit`, `handleConfirmBulkMove`, `handleDelete`, `handleDeleteFolderProducts`, `handleExportExcel`, `onCancel`, `onChange`, `onClick`, `onConfirm`, `onOk`, `onRefresh`, `onSelectFolder`, `onSuccess`

Validation / state evidence: `loading` (7), `error` (7), `Modal` (40), `Table` (6). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## ShipmentPage

Source: `frontend/src/pages/ShipmentPage.tsx`. Roles: Admin, Xnk, XnkManager, KeToan (history); fallback shipment/history.

Purpose / visible actions: handleOpenSizeModal(index, record)}
              >
                + Thêm Size; }
              disabled=; }
            disabled=; }
                  className="p-1 h-auto"
                  onClick=; }
                className="text-xs border-slate-300 text-slate-700 hover:text-blue-600"
                onClick=; }
                className="text-xs border-blue-200 text-blue-700 bg-blue-50/50 hover:bg-blue-50 font-medium"
                onClick=; } aria-label="Thao tác khác" />; } disabled=; Sao chép thành INV mới; } onClick=; Modal.confirm(; Lịch sử chứng từ xuất hàng; }
                onClick=; }
                  onClick=; }
                        onClick=; } onClick=; Lập Invoice & Packing List (INV & PKL); }
                onClick=; }
                loading=; }
                loading=; }
                onClick=; }
                loading=; }
                  className="xl:hidden text-xs h-8 px-2.5 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
                >
                  Khác; }
                      loading=; }
                      onClick=; } onClick=; }
                  disabled=; } disabled=; } disabled=; }
                    disabled=; }
                  disabled=; }
                    className="text-xs h-8 px-2.5 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
                  >
                    Thao tác khác; }
                  disabled=; }
                      onClick=; Giữ nguyên; }
                          disabled=; }
                          disabled=; }
                          disabled=; setExportSequenceModalVisible(false)} className="text-xs h-9 px-4">
            Hủy bỏ; }
            loading=; Tiếp nhận

API and child dependencies: `antd/es/table`, `@ant-design/icons`, `dayjs`, `../api/shipmentApi`, `../api/productMasterApi`, `../api/masterDataFolderApi`, `../api/warehouseApi`, `../api/templateApi`, `../services/tourService`, `../api/customsApi`, `../contexts/AuthContext`, `../hooks/useInvoiceDrafts`, `../services/invoiceDraftStore`, `../types/auth`, `../layouts/AppLayout`, `../types/warehouse`, `../types`, `../types`, `../components/PklPreviewModal`, `../components/QuickPasteModal`, `../components/OcrUploadModal`, `../components/BatchOcrModal`, `../components/CustomsSyncModal`, `../components/QuickAddMasterDataModal`, `../components/DocumentPreviewModal`, `../components/SizeBreakdownModal`, `../components/UnlockAuditsModal`, `../components/TemplateConfigModal`, `../components/CustomsArchiveTreePanel`, `../features/shipment-dispatch/components/SplitMatrixModal`

Handlers (save/submit/export/navigation contracts retained): `applyPartnerPreset`, `deleteShipment`, `exportDate`, `exportPriority`, `exportSavedShipmentExcel`, `exportSequenceModalVisible`, `exportShipmentExcel`, `exportSummary`, `handleAddItem`, `handleApplyOcr`, `handleApplyQuickPaste`, `handleAutoSwitchPartner`, `handleBatchPrint`, `handleClearAllItems`, `handleClearSelection`, `handleConfirmSequenceExport`, `handleDownloadCustomsAttachment`, `handleDownloadHistorical`, `handleExportExcel`, `handleGenerateInvoiceNo`, `handleInvoiceNoChange`, `handleItemChange`, `handleKeepCurrentPartner`, `handleLoadHistoricalOrder`, `handleOpenCustomsSync`, `handleOpenQuickAdd`, `handleOpenSizeModal`, `handlePartnerSelect`, `handlePopulateAllMasterProducts`, `handlePreviewDocument`, `handlePreviewPkl`, `handleProcessTypeChange`, `handleProductSelect`, `handleQuickAddSuccess`, `handleRemoveItem`, `handleResetToNewOrder`, `handleSaveShipment`, `handleSaveSizeBreakdown`, `handleSelectWarehouseBatch`, `handleSetSequence`, `handleUnlockClearedShipment`, `loadHistoricalOrder`, `loadPartnerFolders`, `loadPendingWarehouseBatches`, `loadProducts`, `loadSequence`, `loadShipmentsHistory`, `loadTemplates`, `onApply`, `onCancel`, `onChange`, `onClick`, `onClose`, `onDispatchExported`, `onExportExcel`, `onExported`, `onKeyDown`, `onOk`, `onPartnerChange`, `onSave`, `onSelect`, `onSelectNode`, `onSuccess`, `onSyncSuccess`, `onTabChange`, `onTemplateUpdated`, `onValuesChange`, `removeEventListener`, `validateFields`, `validateItems`

Validation / state evidence: `rules=` (5), `required` (5), `disabled=` (28), `loading` (21), `error` (37), `onKeyDown` (1), `Modal` (147), `Table` (21). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## WarehousePage

Source: `frontend/src/pages/WarehousePage.tsx`. Roles: Admin, Kho.

Purpose / visible actions: Lưới xuất kho thành phẩm (成品鞋出货交接单); }
            onClick=; }
            onClick=; }
            loading=; }
              loading=; }
              onClick=; }
              onClick=; }
            onClick=; }
              onClick=; }
              onClick=; }
              onClick=; }
              onClick=; }
              onClick=; }
                        onClick=; + Thêm vào bảng; }
                          onClick=

API and child dependencies: `@ant-design/icons`, `dayjs`, `dayjs`, `../api/warehouseApi`, `../api/masterDataFolderApi`, `../types/warehouse`, `../types`, `../contexts/AuthContext`

Handlers (save/submit/export/navigation contracts retained): `applyPastedText`, `deleteBatch`, `deleteRow`, `exportDate`, `handleCreateNewBatch`, `handleDeleteBatch`, `handleFolderChange`, `handleGridPaste`, `handleKeyDown`, `handleNoteChange`, `handlePickProduct`, `handleProcessTypeChange`, `handleQuantityChange`, `handleQuantityKeyDown`, `handleSaveDraft`, `handleStyleCodeChange`, `handleStyleCodeFocus`, `handleStyleCodeKeyDown`, `handleStyleCodeSelect`, `handleSubmitToXnk`, `loadBatchToGrid`, `loadHistory`, `loadProductsForFolder`, `onCancel`, `onChange`, `onClick`, `onClose`, `onConfirm`, `onFocus`, `onKeyDown`, `onOk`, `onPaste`, `onSelect`, `removeEventListener`, `saveBatch`, `submitBatch`

Validation / state evidence: `disabled=` (17), `loading` (6), `error` (16), `onKeyDown` (2), `Modal` (17), `Drawer` (11), `Table` (3). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## ExampleTemplateEditor

Source: `frontend/src/features/universal-doc-engine/ExampleTemplateEditor.tsx`. Roles: Inherited from owning workflow; server is authoritative.

Purpose / visible actions: setFields(session.schemaDraft?.fields ?? [])}>Discard schema edits; Apply schema corrections; Validate manual sample

API and child dependencies: `./studioApi`

Handlers (save/submit/export/navigation contracts retained): `onChange`, `onClick`, `onDirtyChange`, `onSubmit`

Validation / state evidence: `required` (5), `disabled=` (17), `unsaved` (1), `revision` (2), `approval` (1). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## OfficeEditors

Source: `frontend/src/features/universal-doc-engine/OfficeEditors.tsx`. Roles: Inherited from owning workflow; server is authoritative.

Purpose / visible actions: Hoàn tác thay đổi; Các mục tìm thấy trong mẫu; Áp dụng các mục dữ liệu; Mục dữ liệu → Sheet → Ô hoặc cột; setSelected(i)}>; =3} type="button" onClick=; Áp dụng vị trí Excel; Quy tắc kiểm tra đã nhập; setRules(old=>old.filter((_,n)=>i!==n))}>Xóa quy tắc; setRules(old=>; =8} type="button" onClick=; Áp dụng quy tắc; Nhập dữ liệu chứng từ; =20} onClick=; Gửi dữ liệu để kiểm tra

API and child dependencies: `./studioApi`, `./officeModel`

Handlers (save/submit/export/navigation contracts retained): `onChange`, `onClick`, `onSubmit`

Validation / state evidence: `required` (12), `disabled=` (36), `error` (1). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## OfficeWizard

Source: `frontend/src/features/universal-doc-engine/OfficeWizard.tsx`. Roles: Authenticated + server membership.

Purpose / visible actions: Trợ lý Excel văn phòng; Synthetic Demo / kỹ thuật; Đăng xuất; navigate(i)}>; void run(async()=>; void run(async()=>; Tải và phân tích mẫu; Đối chiếu ví dụ; action('mapping/confirm')}>Xác nhận vị trí Excel; Quy tắc hiện tại; Kết quả kiểm tra từ máy chủ; Xem dữ liệu và vị trí dự kiến; Tóm tắt trước phê duyệt; action('review')}>Hoàn tất rà soát; action('approve')}>Phê duyệt dữ liệu; void run(async()=>; navigate(step-1)}>Bước trước; navigate(step+1)}>Bước tiếp theo; Bản nháp của tôi; void run(async()=>; void run(async()=>; void run(async()=>; Bản nháp Excel; void run(async()=>; void run(async()=>

API and child dependencies: `../../contexts/AuthContext`, `./studioApi`, `./OfficeEditors`, `./officeModel`

Handlers (save/submit/export/navigation contracts retained): `deleteDraft`, `onChange`, `onClick`, `onDemo`, `onSubmit`, `removeEventListener`, `removeItem`, `resumeDraft`, `saveDraft`

Validation / state evidence: `required` (2), `disabled=` (22), `error` (5), `unsaved` (21), `revision` (9). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## UniversalDocStudio

Source: `frontend/src/features/universal-doc-engine/UniversalDocStudio.tsx`. Roles: Authenticated + server membership.

Purpose / visible actions: Apply mapping correction; Save; Universal Document Studio; Về hướng dẫn Excel; Sign out; My drafts; void run(async () =>; void run(async () =>; void run(async () =>; setSavedDraft(; void run(async () =>; void run(async () =>; changeStep(index)}>; void run(async () =>; void run(async () =>; void run(async () =>; void run(async () => save(await api.sample(session?.workflow === 'example-template' ? workflow : session?.workflow || workflow), 'synthetic-template.xlsx'))}>Download synthetic template; Upload and analyze; Compare filled example; act('mapping/confirm')}>Confirm mapping as current user; Demo Extraction; act('extract-demo')}>Run Demo Extraction; Document Upload; Upload source; void run(async () =>; Validation results; Validation and extraction issues; act('review')}>Complete human review; act('approve')}>Approve document; void run(async () =>; changeStep(step - 1)}>Previous step; changeStep(step + 1)}>Next step

API and child dependencies: `../../contexts/AuthContext`, `./studioApi`, `./WorkflowConfigurationEditor`, `./ExampleTemplateEditor`

Handlers (save/submit/export/navigation contracts retained): `deleteDraft`, `deleteSource`, `onChange`, `onClick`, `onDemo`, `onDirtyChange`, `onOffice`, `onSubmit`, `removeItem`, `resumeDraft`, `saveDraft`

Validation / state evidence: `required` (1), `disabled=` (24), `loading` (1), `error` (8), `unsaved` (2), `revision` (12), `approval` (12). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## WorkflowConfigurationEditor

Source: `frontend/src/features/universal-doc-engine/WorkflowConfigurationEditor.tsx`. Roles: Inherited from owning workflow; server is authoritative.

Purpose / visible actions: Selected data worksheets; setSheets(session.mappings ?? [])}>Discard worksheet mapping edits; setSheets(sheets.filter((_, i) => i !== index))}>Remove worksheet; = 3} onClick=; Apply complete worksheet mapping; Business consistency rules; setRules(session.validationRules ?? [])}>Discard validation rule edits; setRules(rules.filter((_, i) => i !== index))}>Remove rule; = 8} onClick=; Apply validation rules

API and child dependencies: `./studioApi`

Handlers (save/submit/export/navigation contracts retained): `onChange`, `onClick`, `onDirtyChange`, `onSubmit`

Validation / state evidence: `required` (5), `disabled=` (21), `error` (1), `approval` (1). Keyboard behavior must retain native input/form semantics and existing grid handlers. Empty/error/loading branches remain owned by this screen.

## Shared presentation components

- `frontend/src/components/BatchOcrModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/BomFormModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/BomResultTable.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/CheatsheetModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/CustomsArchiveTreePanel.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/CustomsSyncModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/DocumentPreviewModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/FolderConfigModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/FolderTreePanel.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/Navbar.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/OcrUploadModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/OriginalImagePreview.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/PklPreviewModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ProductImportModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ProductionOrderForm.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ProductModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/QuickAddMasterDataModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/QuickPasteModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/SettlementTreePanel.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/SizeBreakdownModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/SizeMatrixInput.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/TemplateConfigModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/UnlockAuditsModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ocr/ManualOcrConfirmationModal.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ocr/OcrDispatchStep.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ocr/OcrDoneStep.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ocr/OcrExportStep.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ocr/OcrPartnerSelector.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ocr/OcrResultStep.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ocr/OcrReviewStep.tsx` — retain owning page handlers, field rules and dialogs.
- `frontend/src/components/ocr/OcrWorkflowStepper.tsx` — retain owning page handlers, field rules and dialogs.