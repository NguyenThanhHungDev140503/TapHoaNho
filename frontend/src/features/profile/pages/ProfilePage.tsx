import React, { useState } from 'react';
import { Alert, Button, Card, Descriptions, Space, Typography, message } from 'antd';
import { UserOutlined, LogoutOutlined } from '@ant-design/icons';
import { useAuthStore, logout } from '../../auth/store/authStore';
import { ProfileOrdersTable } from '../components/ProfileOrdersTable';

const { Title } = Typography;

const ROLE_LABELS: Record<string, string> = {
  Admin: 'Quản trị viên',
  Staff: 'Nhân viên',
};

export const ProfilePage: React.FC = () => {
  const [logoutLoading, setLogoutLoading] = useState(false);

  const user = useAuthStore((s) => s.user);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);

  const roleLabel = user ? (ROLE_LABELS[user.role] ?? 'N/A') : 'N/A';

  const handleLogout = async () => {
    try {
      setLogoutLoading(true);
      // Clears auth state, rotates DPoP key, redirects to IdentityServer end_session.
      await logout();
    } catch {
      message.warning('Đã đăng xuất khỏi ứng dụng');
    } finally {
      setLogoutLoading(false);
    }
  };

  if (!isAuthenticated || !user) {
    return (
      <div style={{ padding: '24px' }}>
        <Alert
          message="Bạn chưa đăng nhập"
          description="Vui lòng đăng nhập để xem thông tin hồ sơ cá nhân."
          type="warning"
          showIcon
        />
      </div>
    );
  }

  // Backend user IDs are numeric; OIDC `sub` is the same value as a string.
  const numericUserId = Number(user.sub);

  return (
    <div style={{ padding: '24px' }}>
      <Space direction="vertical" size="large" style={{ width: '100%' }}>
        <Card>
          <Space align="start" size="middle">
            <UserOutlined style={{ fontSize: '48px', color: '#1890ff' }} />
            <div>
              <Title level={2} style={{ margin: 0 }}>
                Hồ sơ cá nhân
              </Title>
              <Typography.Text type="secondary">
                Thông tin tài khoản của bạn
              </Typography.Text>
            </div>
          </Space>
        </Card>

        <Card
          title="Thông tin tài khoản"
          extra={
            <Button
              type="primary"
              danger
              icon={<LogoutOutlined />}
              onClick={handleLogout}
              loading={logoutLoading}
            >
              Đăng xuất
            </Button>
          }
        >
          <Descriptions column={1} bordered>
            <Descriptions.Item label="Tên đăng nhập">{user.username}</Descriptions.Item>
            <Descriptions.Item label="Họ và tên">{user.fullName}</Descriptions.Item>
            <Descriptions.Item label="Vai trò">{roleLabel}</Descriptions.Item>
            <Descriptions.Item label="ID người dùng">{user.sub}</Descriptions.Item>
          </Descriptions>
        </Card>

        {Number.isFinite(numericUserId) ? (
          <Card title="Đơn hàng đã tạo">
            <ProfileOrdersTable userId={numericUserId} />
          </Card>
        ) : (
          <Alert
            type="info"
            showIcon
            message="Đơn hàng không khả dụng"
            description="Tài khoản này không có ID dạng số nên không thể tải lịch sử đơn hàng."
          />
        )}
      </Space>
    </div>
  );
};
