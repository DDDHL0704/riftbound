import { BehaviorSpec, KeywordCoverageReport } from "../types/catalog";
import { PublicMatchDto } from "../types/protocol";

export type HealthResponse = {
  status: string;
  service: string;
  role: string;
  dotnet: string;
};

export type PreconstructedDeck = {
  id: string;
  name: string;
  description: string;
  legendCardNo: string;
  championCardNo: string;
  mainDeck: string[];
  runeDeck: string[];
  battlefields: string[];
};

export class ApiClient {
  constructor(private readonly serverUrl: string) {}

  async health(signal?: AbortSignal): Promise<HealthResponse> {
    return this.get<HealthResponse>("/health", signal);
  }

  async behaviorSpecs(signal?: AbortSignal): Promise<BehaviorSpec[]> {
    return this.get<BehaviorSpec[]>("/catalog/behavior-specs", signal);
  }

  async keywordCoverage(signal?: AbortSignal): Promise<KeywordCoverageReport> {
    return this.get<KeywordCoverageReport>("/catalog/keyword-coverage", signal);
  }

  async preconstructedDecks(signal?: AbortSignal): Promise<PreconstructedDeck[]> {
    return this.get<PreconstructedDeck[]>("/decks/preconstructed", signal);
  }

  async publicMatches(signal?: AbortSignal): Promise<PublicMatchDto[]> {
    return this.get<PublicMatchDto[]>("/matches", signal);
  }

  private async get<T>(path: string, signal?: AbortSignal): Promise<T> {
    const response = await fetch(`${apiBase(this.serverUrl)}${path}`, { signal });
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`);
    }

    return (await response.json()) as T;
  }
}

export function apiBase(serverUrl: string): string {
  return serverUrl.trim().replace(/\/+$/, "");
}
