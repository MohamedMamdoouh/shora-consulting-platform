export interface ErrorCatalogEntry {
  code: string;
  status: number;
  title: string;
  summary: string;
  type: string;
  whenItOccurs?: string;
  relatedEndpoint?: string;
}

export interface ErrorCatalogListResponse {
  items: ErrorCatalogEntry[];
}
