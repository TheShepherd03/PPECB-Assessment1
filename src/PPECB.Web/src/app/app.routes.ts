import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'products' },

  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login').then(m => m.LoginComponent),
    title: 'Sign in · PPECB'
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register').then(m => m.RegisterComponent),
    title: 'Register · PPECB'
  },

  {
    path: 'categories',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/categories/category-list').then(m => m.CategoryListComponent),
    title: 'Categories · PPECB'
  },
  {
    path: 'categories/new',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/categories/category-form').then(m => m.CategoryFormComponent),
    title: 'Add category · PPECB'
  },
  {
    path: 'categories/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/categories/category-form').then(m => m.CategoryFormComponent),
    title: 'Edit category · PPECB'
  },

  {
    path: 'products',
    canActivate: [authGuard],
    loadComponent: () => import('./features/products/product-list').then(m => m.ProductListComponent),
    title: 'Products · PPECB'
  },
  {
    path: 'products/new',
    canActivate: [authGuard],
    loadComponent: () => import('./features/products/product-form').then(m => m.ProductFormComponent),
    title: 'Add product · PPECB'
  },
  {
    path: 'products/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/products/product-form').then(m => m.ProductFormComponent),
    title: 'Edit product · PPECB'
  },

  { path: '**', redirectTo: 'products' }
];
