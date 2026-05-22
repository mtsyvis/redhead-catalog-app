import { apiClient } from './api.client';
import type {
  CreateTableCustomViewPayload,
  SetActiveTableViewPayload,
  TableCustomView,
  TableViewsResponse,
  UpdateTableCustomViewPayload,
} from '../types/tableViews.types';

class TableViewsService {
  private readonly baseUrl = '/api/me/table-views';

  async getTableViews(tableKey: string): Promise<TableViewsResponse> {
    return apiClient.get<TableViewsResponse>(`${this.baseUrl}/${tableKey}`);
  }

  async setActiveView(tableKey: string, payload: SetActiveTableViewPayload): Promise<void> {
    await apiClient.put<Record<string, never>, SetActiveTableViewPayload>(
      `${this.baseUrl}/${tableKey}/active`,
      payload
    );
  }

  async createCustomView(
    tableKey: string,
    payload: CreateTableCustomViewPayload
  ): Promise<TableCustomView> {
    return apiClient.post<TableCustomView, CreateTableCustomViewPayload>(
      `${this.baseUrl}/${tableKey}/custom`,
      payload
    );
  }

  async updateCustomView(
    tableKey: string,
    id: string,
    payload: UpdateTableCustomViewPayload
  ): Promise<TableCustomView> {
    return apiClient.put<TableCustomView, UpdateTableCustomViewPayload>(
      `${this.baseUrl}/${tableKey}/custom/${encodeURIComponent(id)}`,
      payload
    );
  }

  async deleteCustomView(tableKey: string, id: string): Promise<void> {
    await apiClient.delete<Record<string, never>>(
      `${this.baseUrl}/${tableKey}/custom/${encodeURIComponent(id)}`
    );
  }
}

export const tableViewsService = new TableViewsService();
