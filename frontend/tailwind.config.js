/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#f0f7ff',
          100: '#e0effe',
          500: '#1677ff',
          600: '#0958d9',
          700: '#003eb3',
        }
      }
    },
  },
  plugins: [],
  corePlugins: {
    preflight: false, // avoid conflicts with Ant Design styles
  }
}
