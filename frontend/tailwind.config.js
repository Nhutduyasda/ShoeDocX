/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          50: '#EFF6FF',
          100: '#DBEAFE',
          500: '#2563EB',
          600: '#1D4ED8',
          700: '#1E40AF',
        },
        surface: {
          DEFAULT: '#FFFFFF',
          secondary: '#F9FAFB',
          page: '#F8FAFC',
        },
        border: {
          DEFAULT: '#E5E7EB',
          strong: '#D1D5DB',
          light: '#F3F4F6',
        }
      },
      boxShadow: {
        'enterprise': '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
      },
      borderRadius: {
        'enterprise-sm': '6px',
        'enterprise-md': '8px',
      }
    },
  },
  plugins: [],
  corePlugins: {
    preflight: false, // avoid conflicts with Ant Design styles
  }
}
