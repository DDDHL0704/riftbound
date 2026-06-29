import { BehaviorSpec, KeywordCoverageReport } from "../types/catalog";
import { LeaderboardEntryDto, PlayerMatchDto, PlayerProfileDto, PublicMatchDto } from "../types/protocol";

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

  async playerProfile(handle: string, signal?: AbortSignal): Promise<PlayerProfileDto> {
    return this.get<PlayerProfileDto>(`/players/${encodeURIComponent(handle)}`, signal);
  }

  async playerMatches(handle: string, limit?: number, signal?: AbortSignal): Promise<PlayerMatchDto[]> {
    const query = typeof limit === "number" ? `?limit=${encodeURIComponent(String(limit))}` : "";
    return this.get<PlayerMatchDto[]>(`/players/${encodeURIComponent(handle)}/matches${query}`, signal);
  }

  async leaderboard(limit?: number, signal?: AbortSignal): Promise<LeaderboardEntryDto[]> {
    const query = typeof limit === "number" ? `?limit=${encodeURIComponent(String(limit))}` : "";
    return this.get<LeaderboardEntryDto[]>(`/leaderboard${query}`, signal);
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
