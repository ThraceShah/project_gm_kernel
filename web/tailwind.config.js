/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        cad: {
          dark: '#0f172a',
          canvas: '#131823',
          border: '#334155',
          accent: '#0284c7',
        }
      }
    },
  },
  plugins: [],
}
