import React, { useState } from 'react';
import { Button, Space } from 'antd';
import { CompressOutlined, MinusOutlined, PlusOutlined } from '@ant-design/icons';

interface OriginalImagePreviewProps {
  src: string;
  alt: string;
  maxHeightClassName?: string;
  sourceRegion?: { x?: number | null; y?: number | null; width?: number | null; height?: number | null } | null;
}

export const OriginalImagePreview: React.FC<OriginalImagePreviewProps> = ({
  src,
  alt,
  maxHeightClassName = 'max-h-[560px]',
  sourceRegion,
}) => {
  const [fitImage, setFitImage] = useState(true);
  const [zoom, setZoom] = useState(100);

  const setZoomLevel = (value: number) => {
    setFitImage(false);
    setZoom(Math.min(300, Math.max(25, value)));
  };

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-xs text-slate-500">
          Ảnh gốc · zoom chỉ thay đổi hiển thị
        </span>
        <Space.Compact>
          <Button size="small" icon={<MinusOutlined />} onClick={() => setZoomLevel((fitImage ? 100 : zoom) - 25)} aria-label="Thu nhỏ" />
          <Button size="small" onClick={() => setZoomLevel(zoom)}>{fitImage ? 'Fit' : `${zoom}%`}</Button>
          <Button size="small" icon={<PlusOutlined />} onClick={() => setZoomLevel((fitImage ? 100 : zoom) + 25)} aria-label="Phóng to" />
          <Button size="small" icon={<CompressOutlined />} onClick={() => setFitImage(true)}>Fit Image</Button>
          <Button size="small" onClick={() => setZoomLevel(100)}>100%</Button>
        </Space.Compact>
      </div>
      <div className={`w-full ${maxHeightClassName} overflow-auto border border-slate-200 rounded-lg bg-slate-50`}>
        <div className="relative mx-auto w-fit">
          <img src={src} alt={alt} draggable={false}
            className={fitImage ? 'block max-w-full h-auto object-contain' : 'block max-w-none h-auto object-contain'}
            style={fitImage ? undefined : { width: `${zoom}%` }} />
          {sourceRegion && sourceRegion.x != null && sourceRegion.y != null && sourceRegion.width != null && sourceRegion.height != null &&
            <div aria-label="Vùng OCR đang chọn" className="absolute border-4 border-red-500 bg-red-400/15 shadow-[0_0_0_9999px_rgba(15,23,42,0.18)] pointer-events-none"
              style={{ left: `${sourceRegion.x * 100}%`, top: `${sourceRegion.y * 100}%`, width: `${sourceRegion.width * 100}%`, height: `${sourceRegion.height * 100}%` }} />}
        </div>
      </div>
    </div>
  );
};
