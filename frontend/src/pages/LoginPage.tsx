import React, { useState } from 'react';
import { Form, Input, Button, Checkbox, Card, Tag } from 'antd';
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

  const handleQuickLogin = (username: string, password: string) => {
    form.setFieldsValue({ username, password, rememberMe: true });
    form.submit();
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col justify-center items-center p-4 relative">
      {/* Background subtle elements */}
      <div className="absolute inset-0 bg-[radial-gradient(#cbd5e1_1px,transparent_1px)] [background-size:16px_16px] opacity-40 pointer-events-none" />

      <div className="w-full max-w-md relative z-10">
        {/* Header Branding */}
        <div className="text-center mb-6">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-2xl bg-blue-600 text-white font-black text-2xl mb-3 shadow-md shadow-blue-500/20">
            SD
          </div>
          <h1 className="text-2xl font-extrabold text-slate-900 tracking-tight m-0">
            ShoeDocX Enterprise
          </h1>
          <p className="text-xs text-slate-500 mt-1 m-0">
            Hệ thống Quản trị & Giao nhận Gia công Xuất khẩu E52 CMT/DAP
          </p>
          <div className="text-[11px] text-slate-400 mt-0.5">
            Nhà máy Kingmaker III • Liên thông Kho – XNK – Kế toán
          </div>
        </div>

        {/* Login Card */}
        <Card className="shadow-lg border-slate-200 bg-white rounded-xl text-slate-800 p-2">
          <div className="mb-4 pb-3 border-b border-slate-100 flex items-center justify-between">
            <span className="text-sm font-bold text-slate-800">Đăng nhập tài khoản</span>
            <Tag color="blue" className="m-0 text-[11px] font-mono">Xác thực Identity</Tag>
          </div>

          <Form
            form={form}
            layout="vertical"
            initialValues={{ rememberMe: true }}
            onFinish={handleSubmit}
            size="large"
          >
            <Form.Item
              name="username"
              rules={[{ required: true, message: 'Vui lòng nhập tên đăng nhập' }]}
            >
              <Input
                prefix={<UserOutlined className="text-slate-400" />}
                placeholder="Tên đăng nhập (admin, xnk, kho, ketoan)"
                className="rounded-lg text-xs"
                autoComplete="username"
              />
            </Form.Item>

            <Form.Item
              name="password"
              rules={[{ required: true, message: 'Vui lòng nhập mật khẩu' }]}
            >
              <Input.Password
                prefix={<LockOutlined className="text-slate-400" />}
                placeholder="Mật khẩu"
                className="rounded-lg text-xs"
                autoComplete="current-password"
              />
            </Form.Item>

            <div className="flex items-center justify-between mb-4">
              <Form.Item name="rememberMe" valuePropName="checked" noStyle>
                <Checkbox className="text-xs text-slate-600">
                  Ghi nhớ đăng nhập trên máy này
                </Checkbox>
              </Form.Item>
            </div>

            <Button
              type="primary"
              htmlType="submit"
              icon={<LoginOutlined />}
              loading={submitting}
              className="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold text-sm h-10 rounded-lg shadow-sm"
            >
              Đăng nhập hệ thống
            </Button>
          </Form>

          {/* Quick Login Section */}
          <div className="mt-5 pt-4 border-t border-slate-100">
            <div className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider mb-2 text-center">
              Chọn nhanh vai trò để thử nghiệm
            </div>

            <div className="grid grid-cols-2 gap-2 text-xs">
              <button
                type="button"
                onClick={() => handleQuickLogin('kho', '@Kho123')}
                className="p-2.5 rounded-lg border border-emerald-200 bg-emerald-50 hover:bg-emerald-100/80 text-left transition-all group"
              >
                <div className="flex items-center space-x-1.5 font-bold text-emerald-800">
                  <span>📦</span>
                  <span>Thủ Kho</span>
                </div>
                <div className="text-[10px] text-emerald-600 mt-0.5">Kho Thành Phẩm (Fast-Grid)</div>
              </button>

              <button
                type="button"
                onClick={() => handleQuickLogin('xnk', '@Xnk123')}
                className="p-2.5 rounded-lg border border-blue-200 bg-blue-50 hover:bg-blue-100/80 text-left transition-all group"
              >
                <div className="flex items-center space-x-1.5 font-bold text-blue-800">
                  <span>🚢</span>
                  <span>XNK</span>
                </div>
                <div className="text-[10px] text-blue-600 mt-0.5">Lập Invoice / Packing List</div>
              </button>

              <button
                type="button"
                onClick={() => handleQuickLogin('ketoan', '@KeToan123')}
                className="p-2.5 rounded-lg border border-amber-200 bg-amber-50 hover:bg-amber-100/80 text-left transition-all group"
              >
                <div className="flex items-center space-x-1.5 font-bold text-amber-800">
                  <span>💰</span>
                  <span>Kế toán</span>
                </div>
                <div className="text-[10px] text-amber-600 mt-0.5">Doanh thu & Đối chiếu CMT</div>
              </button>

              <button
                type="button"
                onClick={() => handleQuickLogin('admin', '@Admin123')}
                className="p-2.5 rounded-lg border border-purple-200 bg-purple-50 hover:bg-purple-100/80 text-left transition-all group"
              >
                <div className="flex items-center space-x-1.5 font-bold text-purple-800">
                  <span>👑</span>
                  <span>Giám Đốc</span>
                </div>
                <div className="text-[10px] text-purple-600 mt-0.5">Toàn quyền điều hành</div>
              </button>
            </div>
          </div>
        </Card>

        {/* Footer info */}
        <div className="text-center mt-4 text-xs text-slate-400">
          ShoeDocX • Bản quyền thuộc Nhà máy Kingmaker III &copy; 2026
        </div>
      </div>
    </div>
  );
};
