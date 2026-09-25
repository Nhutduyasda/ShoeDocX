import React from 'react';
import { Select, Space, Tag, Typography } from 'antd';
import { ShopOutlined } from '@ant-design/icons';
import type { MasterDataFolder } from '../../types';

const { Text } = Typography;

interface Props {
  partners: MasterDataFolder[];
  selectedPartnerId?: number | null;
  onPartnerChange: (partnerId: number) => void;
  disabled?: boolean;
}

export const OcrPartnerSelector: React.FC<Props> = ({
  partners,
  selectedPartnerId,
  onPartnerChange,
  disabled = false,
}) => {
  const currentPartner = partners.find((p) => p.id === selectedPartnerId);

  return (
    <Space align="center" className="bg-white px-3 py-1 rounded border border-slate-200 shadow-sm">
      <ShopOutlined className="text-blue-600" />
      <Text className="text-xs font-semibold text-slate-700 whitespace-nowrap">Đối tác / Hợp đồng:</Text>
      <Select
        size="small"
        placeholder="Chọn đối tác..."
        style={{ minWidth: 200 }}
        value={selectedPartnerId ?? undefined}
        onChange={onPartnerChange}
        disabled={disabled}
        options={partners.map((p) => ({
          value: p.id,
          label: (
            <div className="flex justify-between items-center">
              <span>{p.name}</span>
              {p.customerName && (
                <span className="text-[10px] text-slate-400 ml-2">{p.customerName.slice(0, 15)}...</span>
              )}
            </div>
          ),
        }))}
      />
      {currentPartner ? (
        <Tag color="blue" className="text-xs m-0">
          {currentPartner.poSuffix ? `PO: ${currentPartner.poSuffix}` : currentPartner.name}
        </Tag>
      ) : (
        <Tag color="orange" className="text-xs m-0">
          Chưa chọn
        </Tag>
      )}
    </Space>
  );
};
