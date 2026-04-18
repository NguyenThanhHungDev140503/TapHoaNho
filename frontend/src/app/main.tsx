import { StrictMode } from 'react'
import ReactDOM from 'react-dom/client'
import './index.css'
import { RouterProvider } from '@tanstack/react-router';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import 'antd/dist/reset.css';
import { router } from './routes/routeTree'; // Import the router from routeTree
import { queryClient } from '../lib/query/queryClient';
import { useAuthStore } from '../features/auth/store/authStore';

// Restore OIDC session from oidc-client-ts session storage on app boot.
// This is async but intentionally fire-and-forget here — the authStore
// sets isLoading=true initially and flips it false once done, so
// any protected route can gate on isLoading.
useAuthStore.getState().initFromSession();

// Render the app
const rootElement = document.getElementById('root')!
if (!rootElement.innerHTML) {
  const root = ReactDOM.createRoot(rootElement)
  root.render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </StrictMode>,
  )
}
