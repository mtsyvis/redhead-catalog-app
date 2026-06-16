import type { ApiError } from '../types/auth.types';
import { notifySessionExpired } from './sessionExpired';

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
  fieldErrors?: Record<string, string[]>;

  constructor(
    message: string,
    statusCode: number,
    errors?: string[],
    fieldErrors?: Record<string, string[]>
  ) {
    super(message);
    this.name = 'ApiClientError';
    this.statusCode = statusCode;
    this.errors = errors;
    this.fieldErrors = fieldErrors;
  }
}

/**
 * API client for making authenticated requests
 */
export class ApiClient {
  private static shouldNotifySessionExpired(endpoint: string): boolean {
    return endpoint !== '/api/auth/login' && endpoint !== '/api/auth/me';
  }

  private static isFieldErrorMap(value: unknown): value is Record<string, string[]> {
    return (
      !!value &&
      typeof value === 'object' &&
      !Array.isArray(value) &&
      Object.values(value).every(
        (item) => Array.isArray(item) && item.every((message) => typeof message === 'string')
      )
    );
  }

  private static async handleResponse<T>(response: Response, endpoint: string): Promise<T> {
    if (!response.ok) {
      if (response.status === 401 && this.shouldNotifySessionExpired(endpoint)) {
        notifySessionExpired();
      }

      let errorData: ApiError | null = null;

      try {
        errorData = await response.json();
      } catch {
        // If JSON parsing fails, use status text
      }

      const responseErrors = errorData?.errors;
      const errors = Array.isArray(responseErrors) ? responseErrors : undefined;
      const fieldErrors = errorData?.fieldErrors ?? (
        this.isFieldErrorMap(responseErrors) ? responseErrors : undefined
      );
      const message =
        errorData?.message ||
        errorData?.detail ||
        errorData?.title ||
        response.statusText ||
        'An error occurred';

      throw new ApiClientError(message, response.status, errors, fieldErrors);
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

    return this.handleResponse<T>(response, endpoint);
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

    return this.handleResponse<T>(response, endpoint);
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

    return this.handleResponse<T>(response, endpoint);
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

    return this.handleResponse<T>(response, endpoint);
  }
}

/**
 * Singleton instance of ApiClient for convenient usage
 */
export const apiClient = ApiClient;
