export function ResultPage({ matchId }: { matchId: string; onNavigate: unknown }) {
  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">结算</span>
          <h1>{matchId}</h1>
          <p>等待服务端 result/winner 字段接入。当前前端不会本地判胜。</p>
        </div>
      </section>
    </div>
  );
}
