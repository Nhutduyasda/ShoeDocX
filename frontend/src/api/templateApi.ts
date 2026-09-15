import { apiClient } from './client';
import type { CompanyTemplate, AiTemplateAnalysisResponse, DocumentPreviewResponse, TenantAiCreditsInfo } from '../types';

export const templateApi = {
  getAll: async (): Promise<CompanyTemplate[]> => {
    const response = await apiClient.get<CompanyTemplate[]>('/templates');
    return response.data;
  },

  getById: async (id: number): Promise<CompanyTemplate> => {
    const response = await apiClient.get<CompanyTemplate>(`/templates/${id}`);
    return response.data;
  },

  getDefault: async (): Promise<CompanyTemplate> => {
    const response = await apiClient.get<CompanyTemplate>('/templates/default');
    return response.data;
  },

  uploadTemplate: async (file: File, name: string, configJson?: string, isDefault?: boolean): Promise<CompanyTemplate> => {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('name', name);
    if (configJson) {
      formData.append('configJson', configJson);
    }
    if (isDefault !== undefined) {
      formData.append('isDefault', String(isDefault));
    }

    const response = await apiClient.post<CompanyTemplate>('/templates', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data;
  },

  getAiCredits: async (): Promise<TenantAiCreditsInfo> => {
    const response = await apiClient.get<TenantAiCreditsInfo>('/templates/ai-credits');
    return response.data;
  },

  addDemoCredits: async (): Promise<{ aiCredits: number; message: string }> => {
    const response = await apiClient.post<{ aiCredits: number; message: string }>('/templates/ai-credits/add-demo');
    return response.data;
  },

  aiAnalyze: async (file: File): Promise<AiTemplateAnalysisResponse> => {
    const formData = new FormData();
    formData.append('file', file);

    const response = await apiClient.post<AiTemplateAnalysisResponse>('/templates/ai-analyze', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data;
  },

  previewWithConfig: async (file: File | null, configJson: string, templateId?: number | null): Promise<DocumentPreviewResponse> => {
    const formData = new FormData();
    if (file) {
      formData.append('file', file);
    }
    formData.append('configJson', configJson);
    if (templateId !== undefined && templateId !== null) {
      formData.append('templateId', String(templateId));
    }

    const response = await apiClient.post<DocumentPreviewResponse>('/templates/preview-with-config', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data;
  },

  updateConfig: async (id: number, configJson: string): Promise<void> => {
    await apiClient.put(`/templates/${id}/config`, { configJson });
  },

  setDefault: async (id: number): Promise<void> => {
    await apiClient.put(`/templates/${id}/set-default`);
  },

  downloadTemplate: async (id: number, fileName: string): Promise<void> => {
    const response = await apiClient.get(`/templates/download/${id}`, {
      responseType: 'blob',
    });
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', fileName || `Template_${id}.xlsx`);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },
};
