import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The API's development CORS policy accepts loopback origins on ports 5173-5179.
export default defineConfig({
  plugins: [react()],
  server: { host: "127.0.0.1", port: 5176, strictPort: true },
  preview: { host: "127.0.0.1", port: 5177, strictPort: true },
});
