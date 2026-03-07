# Project Coding Rules (Non-Obvious Only)

- Always use safeWriteJson() from src/utils/ instead of JSON.stringify for file writes (prevents corruption)
- API retry mechanism in src/api/providers/utils/ is mandatory (not optional as it appears)
- Database queries MUST use the query builder in packages/evals/src/db/queries/ (raw SQL will fail)
- Provider interface in packages/types/src/ has undocumented required methods
- Test files must be in same directory as source for vitest to work (not in separate test folder)
- TanStack Router code-based routing requires generated routeTree.gen.ts to be updated when routes change
- React Compiler plugin (babel-plugin-react-compiler) must be enabled for performance optimization
- Frontend API calls must go through pre-configured Axios instance with interceptors for auth handling
- Type-safe endpoint definitions in frontend/src/app/routes/type/endpoint.ts must be maintained for all API endpoints
- Feature hooks in frontend follow use[Feature]Management pattern (e.g., useUserManagement.ts) for consistency