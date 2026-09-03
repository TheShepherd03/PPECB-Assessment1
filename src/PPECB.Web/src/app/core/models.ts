export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface Category {
  categoryId: number;
  name: string;
  categoryCode: string;
  isActive: boolean;
  createdBy: string;
  createdDate: string;
  updatedBy: string | null;
  updatedDate: string | null;
  rowVersion: string | null;
}

export interface CategoryLookup {
  categoryId: number;
  name: string;
  categoryCode: string;
}

export interface Product {
  productId: number;
  productCode: string;
  name: string;
  description: string | null;
  categoryId: number;
  categoryName: string;
  categoryCode: string;
  price: number;
  imagePath: string | null;
  createdBy: string;
  createdDate: string;
  updatedBy: string | null;
  updatedDate: string | null;
  rowVersion: string | null;
}

export interface AuthResponse {
  userId: string;
  email: string;
  token: string;
  expiresAtUtc: string;
}

export interface ExcelImportError {
  rowNumber: number;
  message: string;
}

export interface ExcelImportResult {
  succeeded: boolean;
  rowsRead: number;
  productsImported: number;
  errors: ExcelImportError[];
}

/**
 * Shape of an RFC 7807 response from the API. `errors` is present on validation
 * failures and maps a field name to its messages.
 */
export interface ApiProblem {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
