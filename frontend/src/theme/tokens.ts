/**
 * ShoeDocX Enterprise Design System Tokens
 * Strictly adheres to Enterprise / Professional / Minimal guidelines:
 * - Neutral & Calm palette
 * - Clear visual hierarchy
 * - 6px/8px border radii
 * - 0 1px 2px shadow baseline
 */

export const tokens = {
  // Brand & Primary
  colorPrimary: '#2563EB',
  colorPrimaryHover: '#1D4ED8',
  colorPrimaryActive: '#1E40AF',
  colorPrimaryLight: '#EFF6FF',
  colorPrimaryBorder: '#BFDBFE',

  // Neutrals & Surfaces
  pageBg: '#F6F7F9',
  surface: '#FFFFFF',
  surfaceSecondary: '#F9FAFB',
  surfaceHover: '#F1F5F9',
  surfaceActive: '#E2E8F0',

  // Borders
  border: '#E5E7EB',
  borderStrong: '#D1D5DB',
  borderLight: '#F3F4F6',

  // Text Hierarchy
  textPrimary: '#111827',
  textSecondary: '#4B5563',
  textMuted: '#6B7280',
  textDisabled: '#9CA3AF',
  textInverse: '#FFFFFF',

  // Semantic
  success: '#15803D',
  successLight: '#F0FDF4',
  successBorder: '#BBF7D0',
  successText: '#15803D',

  warning: '#D97706',
  warningLight: '#FFFBEB',
  warningBorder: '#FDE68A',
  warningText: '#B45309',

  error: '#DC2626',
  errorLight: '#FEF2F2',
  errorBorder: '#FECACA',
  errorText: '#B91C1C',

  info: '#2563EB',
  infoLight: '#EFF6FF',
  infoBorder: '#BFDBFE',

  // Sidebar
  sidebarBg: '#172033',
  sidebarBorder: 'rgba(255, 255, 255, 0.08)',
  sidebarText: '#94A3B8',
  sidebarTextActive: '#FFFFFF',
  sidebarItemActiveBg: 'rgba(255, 255, 255, 0.08)',

  // Radii
  radiusXs: 4, // badges, tags
  radiusSm: 6, // buttons, inputs, dropdowns
  radiusMd: 8, // cards, panels, modals
  radiusLg: 8,

  // Shadows
  shadowCard: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
  shadowDropdown: '0 4px 6px -1px rgba(0, 0, 0, 0.07), 0 2px 4px -2px rgba(0, 0, 0, 0.05)',

  // Layout Dimensions
  sidebarWidth: 250,
  sidebarCollapsedWidth: 68,
  headerHeight: 56,
  contentMaxWidth: '100%',
} as const;

