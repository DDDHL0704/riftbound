import { createContext, ReactNode, useContext, useEffect, useMemo, useState } from "react";
import { ApiClient } from "../services/apiClient";
import { BehaviorSpec, KeywordCoverageReport } from "../types/catalog";

type CatalogContextValue = {
  specs: BehaviorSpec[];
  specByNo: Record<string, BehaviorSpec>;
  keywordCoverage?: KeywordCoverageReport;
  loading: boolean;
  error?: string;
  reload: () => void;
};

const CatalogContext = createContext<CatalogContextValue | undefined>(undefined);

export function CatalogProvider({ children, serverUrl }: { children: ReactNode; serverUrl: string }) {
  const [specs, setSpecs] = useState<BehaviorSpec[]>([]);
  const [keywordCoverage, setKeywordCoverage] = useState<KeywordCoverageReport | undefined>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | undefined>();
  const [reloadTick, setReloadTick] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    const client = new ApiClient(serverUrl);
    setLoading(true);
    setError(undefined);

    Promise.all([client.behaviorSpecs(controller.signal), client.keywordCoverage(controller.signal)])
      .then(([nextSpecs, nextCoverage]) => {
        setSpecs(nextSpecs);
        setKeywordCoverage(nextCoverage);
      })
      .catch((nextError: unknown) => {
        if (!controller.signal.aborted) {
          setError(nextError instanceof Error ? nextError.message : String(nextError));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      });

    return () => controller.abort();
  }, [serverUrl, reloadTick]);

  const specByNo = useMemo(
    () =>
      specs.reduce<Record<string, BehaviorSpec>>((map, spec) => {
        map[spec.cardNo] = spec;
        return map;
      }, {}),
    [specs]
  );

  const value = useMemo<CatalogContextValue>(
    () => ({
      specs,
      specByNo,
      keywordCoverage,
      loading,
      error,
      reload: () => setReloadTick((current) => current + 1)
    }),
    [error, keywordCoverage, loading, specByNo, specs]
  );

  return <CatalogContext.Provider value={value}>{children}</CatalogContext.Provider>;
}

export function useCatalog(): CatalogContextValue {
  const value = useContext(CatalogContext);
  if (!value) {
    throw new Error("useCatalog must be used within CatalogProvider");
  }

  return value;
}
