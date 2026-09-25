import React from 'react';
import { Steps } from 'antd';
import {
  FileSearchOutlined,
  BranchesOutlined,
  TableOutlined,
  ExportOutlined,
  CheckCircleOutlined,
} from '@ant-design/icons';

export type OcrWorkflowStep = 'review' | 'dispatch' | 'result' | 'export' | 'done';

export const STEP_KEYS: OcrWorkflowStep[] = ['review', 'dispatch', 'result', 'export', 'done'];

interface Props {
  currentStep: OcrWorkflowStep;
  maxReachedStep: number;
  onStepChange: (step: OcrWorkflowStep) => void;
  canProceedToDispatch: boolean;
  canProceedToResult: boolean;
  canProceedToExport: boolean;
  isDone: boolean;
}

export const OcrWorkflowStepper: React.FC<Props> = ({
  currentStep,
  maxReachedStep,
  onStepChange,
  canProceedToDispatch,
  canProceedToResult,
  canProceedToExport,
  isDone,
}) => {
  const currentIndex = STEP_KEYS.indexOf(currentStep);

  const stepItems = [
    {
      title: '1. OCR & Đối soát',
      icon: <FileSearchOutlined />,
      disabled: false,
    },
    {
      title: '2. Điều phối',
      icon: <BranchesOutlined />,
      disabled: !canProceedToDispatch && maxReachedStep < 1,
    },
    {
      title: '3. Kết quả',
      icon: <TableOutlined />,
      disabled: !canProceedToResult && maxReachedStep < 2,
    },
    {
      title: '4. Xuất chứng từ',
      icon: <ExportOutlined />,
      disabled: !canProceedToExport && maxReachedStep < 3,
    },
    {
      title: '5. Hoàn tất',
      icon: <CheckCircleOutlined />,
      disabled: !isDone,
    },
  ];

  const handleStepClick = (index: number) => {
    // Cho phép quay lại bất kỳ bước nào đã từng đi qua hoặc bước hiện tại
    if (index <= maxReachedStep) {
      onStepChange(STEP_KEYS[index]);
    }
  };

  return (
    <div className="w-full bg-slate-50 border-b border-slate-200 px-6 py-2.5 shadow-sm">
      <Steps
        size="small"
        current={currentIndex}
        onChange={handleStepClick}
        items={stepItems}
        className="ocr-workflow-stepper"
      />
    </div>
  );
};
