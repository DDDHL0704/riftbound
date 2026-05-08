import { Search } from "lucide-react";
import { useMemo, useState } from "react";
import { CardDetailDrawer } from "../components/cards/CardDetailDrawer";
import { CardFace, InspectedCard } from "../components/cards/CardFace";
import { StatusPill } from "../components/ui/StatusPill";
import { useCatalog } from "../stores/catalogStore";
import { conformanceLabel } from "../utils/formatters";

export function CardLibraryPage() {
  const { specs, loading, error, keywordCoverage } = useCatalog();
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState("全部");
  const [tier, setTier] = useState("全部");
  const [inspectedCard, setInspectedCard] = useState<InspectedCard | undefined>();

  const categories = useMemo(() => ["全部", ...Array.from(new Set(specs.map((spec) => spec.cardCategoryName))).sort()], [specs]);
  const tiers = useMemo(() => ["全部", ...Array.from(new Set(specs.map((spec) => spec.conformanceTier))).sort()], [specs]);
  const filtered = useMemo(
    () =>
      specs
        .filter((spec) => category === "全部" || spec.cardCategoryName === category)
        .filter((spec) => tier === "全部" || spec.conformanceTier === tier)
        .filter((spec) => `${spec.cardName} ${spec.cardNo} ${spec.officialText}`.toLowerCase().includes(query.toLowerCase()))
        .slice(0, 80),
    [category, query, specs, tier]
  );

  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">卡牌图鉴</span>
          <h1>官方卡牌视图</h1>
          <p>当前只展示服务端行为规格和关键词覆盖口径，完整规则结论以最终复审为准。</p>
        </div>
        <StatusPill tone={error ? "bad" : "good"}>{loading ? "加载中" : error ? "加载失败" : `${specs.length} 张卡`}</StatusPill>
      </section>
      <section className="filter-bar">
        <label>
          <span><Search size={16} /> 搜索</span>
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="卡名、编号、规则文本" />
        </label>
        <label>
          <span>类型</span>
          <select value={category} onChange={(event) => setCategory(event.target.value)}>
            {categories.map((item) => <option key={item}>{item}</option>)}
          </select>
        </label>
        <label>
          <span>规则证据</span>
          <select value={tier} onChange={(event) => setTier(event.target.value)}>
            {tiers.map((item) => <option key={item} value={item}>{item === "全部" ? item : conformanceLabel(item)}</option>)}
          </select>
        </label>
      </section>
      <section className="coverage-strip">
        <StatusPill tone="warn">待补关键词族：{keywordCoverage?.families.filter((family) => family.deferredCards.length > 0).length ?? 0}</StatusPill>
        <span>图鉴必须暴露待补与代表性状态，不能隐藏服务端证据缺口。</span>
      </section>
      <section className="card-library-grid">
        {filtered.map((spec) => <CardFace key={spec.cardNo} onInspect={setInspectedCard} spec={spec} />)}
      </section>
      <CardDetailDrawer card={inspectedCard} onClose={() => setInspectedCard(undefined)} />
    </div>
  );
}
