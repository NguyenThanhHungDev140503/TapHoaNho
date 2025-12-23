# AGENTS.md

This file provides guidance to agents when working with code in this repository.

## Project Overview
- Full-stack store management system with React frontend and .NET 9 backend
- PostgreSQL database with Entity Framework Core
- JWT authentication and role-based access (Admin/Staff)
- TanStack Router for code-based routing with type safety
- Ant Design UI components with Zustand state management

## Commands
- `cd frontend && yarn dev` - Start frontend development server
- `cd frontend && yarn build` - Build frontend for production
- `cd frontend && yarn lint` - Run ESLint linter
- `cd RetailStoreManagement && dotnet run` - Start backend API server
- `cd RetailStoreManagement && dotnet build` - Build backend

## Code Style & Conventions
- TypeScript strict mode with verbatimModuleSyntax
- ESLint with recommended TypeScript and React hooks rules
- camelCase for JSON properties, C# PascalCase for properties
- Feature-Sliced Design (FSD) architecture in frontend
- Clean Architecture patterns with dependency injection
- API responses use camelCase with ApiResponse<T> wrapper
- Pagination via page/pageSize/search/sortBy parameters returning PagedList<T>

## Non-Obvious Patterns
- TanStack Router code-based routing with generated routeTree.gen.ts
- React Compiler plugin enabled via babel-plugin-react-compiler
- Backend uses Unit of Work pattern with generic repositories
- Frontend API calls through pre-configured Axios instance with interceptors
- Type-safe endpoint definitions in frontend/src/app/routes/type/endpoint.ts
- Feature hooks in frontend follow use[Feature]Management pattern (e.g., useUserManagement.ts)
- Backend controllers inherit from BaseAdminController with JWT authentication
- Frontend forms use Ant Design Form components with built-in validation
- PDF invoice generation from paid order information
- Inventory automatically updates when orders change