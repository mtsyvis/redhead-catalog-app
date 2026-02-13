import type { ApiError } from '../types/auth.types';

/**
 * Base API configuration.
 * In dev we use '' so requests are same-origin and Vite proxy forwards /api to the backend.
 * In production use env or default (same-origin when SPA is served from API).
 */
const API_BASE_URL =
  import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL || '');

/**
 * Custom error class for API errors
 */
export class ApiClientError extends Error {
  statusCode: number;
  errors?: string[];
  
  constructor(
    message: string,
    statusCode: number,
    errors?: string[]
  ) {
    super(message);
    this.name = 'ApiClientError';
    this.statusCode = statusCode;
    this.errors = errors;
  }
}

/**
 * API client for making authenticated requests
 */
export class ApiClient {
  private static async handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      let errorData: ApiError | null = null;

      try {
        errorData = await response.json();
      } catch {
        // If JSON parsing fails, use status text
      }

      const message = errorData?.message || response.statusText || 'An error occurred';
      const errors = errorData?.errors;

      throw new ApiClientError(message, response.status, errors);
    }

    // Handle 204 No Content
    if (response.status === 204) {
      return {} as T;
    }

    return response.json();
  }

  /**
   * Perform GET request
   */
  static async get<T>(endpoint: string): Promise<T> {
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      method: 'GET',
      credentials: 'include', // Important for cookie auth
      headers: {
        'Content-Type': 'application/json',
      },
    });

    return this.handleResponse<T>(response);
  }

  /**
   * Perform POST request
   */
  static async post<T, D = unknown>(endpoint: string, data?: D): Promise<T> {
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
      },
      body: data ? JSON.stringify(data) : undefined,
    });

    return this.handleResponse<T>(response);
  }

  /**
   * Perform PUT request
   */
  static async put<T, D = unknown>(endpoint: string, data: D): Promise<T> {
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      method: 'PUT',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(data),
    });

    return this.handleResponse<T>(response);
  }

  /**
   * Perform DELETE request
   */
  static async delete<T>(endpoint: string): Promise<T> {
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      method: 'DELETE',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    return this.handleResponse<T>(response);
  }
}

/**
 * Singleton instance of ApiClient for convenient usage
 */
export const apiClient = ApiClient;
