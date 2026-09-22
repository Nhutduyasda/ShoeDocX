import React, { useState } from 'react';
import { Button, Space } from 'antd';
import { CompressOutlined, MinusOutlined, PlusOutlined } from '@ant-design/icons';

interface OriginalImagePreviewProps {
  src: string;
  alt: string;
  maxHeightClassName?: string;
}

export const OriginalImagePreview: React.FC<OriginalImagePreviewProps> = ({
  src,
  alt,
  maxHeightClassName = 'max-h-[560px]',
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
        <img
          src={src}
          alt={alt}
          draggable={false}
          className={fitImage ? 'block max-w-full h-auto object-contain mx-auto' : 'block max-w-none h-auto object-contain'}
          style={fitImage ? undefined : { width: `${zoom}%` }}
        />
      </div>
    </div>
  );
};
