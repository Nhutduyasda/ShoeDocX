import React, { useState } from 'react';
import { Form, Input, Button, Checkbox } from 'antd';
import { UserOutlined, LockOutlined, LoginOutlined } from '@ant-design/icons';
import { useAuth } from '../contexts/AuthContext';
import type { LoginRequest } from '../types/auth';

export const LoginPage: React.FC = () => {
  const { login } = useAuth();
  const [form] = Form.useForm();
  const [submitting, setSubmitting] = useState<boolean>(false);

  const handleSubmit = async (values: any) => {
    try {
      setSubmitting(true);
      const req: LoginRequest = {
        username: values.username.trim(),
        password: values.password,
        rememberMe: values.rememberMe ?? true,
      };
      await login(req);
    } catch {
      // Error handled by api client
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#F6F8FA] flex flex-col justify-center items-center p-4">
      <div className="w-full max-w-sm">
        {/* Header Branding */}
        <div className="text-center mb-6">
          <div className="inline-flex items-center justify-center w-10 h-10 rounded-md bg-[#2563EB] text-white font-semibold text-sm mb-3 shadow-xs">
            SD
          </div>
          <h1 className="text-xl font-semibold text-[#111827] tracking-tight m-0">
            ShoeDocX Enterprise
          </h1>
          <p className="text-xs text-[#4B5563] mt-1 m-0">
            Hệ thống Quản trị & Giao nhận Gia công Xuất khẩu E52 CMT/DAP
          </p>
          <div className="text-[11px] text-[#6B7280] mt-0.5">
            Nhà máy Kingmaker III • Liên thông Kho – XNK – Kế toán
          </div>
        </div>

        {/* Login Card */}
        <div className="bg-white border border-[#E5E7EB] rounded-lg p-6 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
          <div className="mb-5 pb-3 border-b border-[#F3F4F6]">
            <div className="text-sm font-semibold text-[#111827]">Đăng nhập hệ thống</div>
            <div className="text-xs text-[#6B7280] mt-0.5">Sử dụng tài khoản nội bộ được cấp quyền.</div>
          </div>

          <Form
            form={form}
            layout="vertical"
            initialValues={{ rememberMe: true }}
            onFinish={handleSubmit}
          >
            <Form.Item
              label={<span className="text-xs font-medium text-[#374151]">Tên đăng nhập</span>}
              name="username"
              rules={[{ required: true, message: 'Vui lòng nhập tên đăng nhập' }]}
              className="mb-4"
            >
              <Input
                prefix={<UserOutlined className="text-[#9CA3AF] text-xs mr-1" />}
                placeholder="Tên đăng nhập"
                className="text-xs h-9"
                autoComplete="username"
              />
            </Form.Item>

            <Form.Item
              label={<span className="text-xs font-medium text-[#374151]">Mật khẩu</span>}
              name="password"
              rules={[{ required: true, message: 'Vui lòng nhập mật khẩu' }]}
              className="mb-4"
            >
              <Input.Password
                prefix={<LockOutlined className="text-[#9CA3AF] text-xs mr-1" />}
                placeholder="Mật khẩu"
                className="text-xs h-9"
                autoComplete="current-password"
              />
            </Form.Item>

            <div className="flex items-center justify-between mb-5">
              <Form.Item name="rememberMe" valuePropName="checked" noStyle>
                <Checkbox className="text-xs text-[#4B5563]">
                  Ghi nhớ đăng nhập
                </Checkbox>
              </Form.Item>
            </div>

            <Button
              type="primary"
              htmlType="submit"
              icon={<LoginOutlined />}
              loading={submitting}
              className="w-full bg-[#2563EB] hover:bg-[#1D4ED8] text-white font-medium text-xs h-9 rounded-md border-none cursor-pointer"
            >
              Đăng nhập
            </Button>
          </Form>
        </div>

        {/* Footer info */}
        <div className="text-center mt-4 text-xs text-[#9CA3AF]">
          ShoeDocX • Bản quyền thuộc Nhà máy Kingmaker III &copy; 2026
        </div>
      </div>
    </div>
  );
};

