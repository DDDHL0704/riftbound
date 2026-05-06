import { SnapshotDto } from "../../types/protocol";
import { asRecord, asString } from "../../utils/collections";

export function StackPanel({ snapshot }: { snapshot?: SnapshotDto }) {
  const stack = snapshot?.stack ?? [];
  const queue = asRecord(asRecord(snapshot?.timing).pendingTaskQueue);

  return (
    <section className="side-panel">
      <header>
        <span className="eyebrow">规则队列</span>
        <h2>结算链 / 任务</h2>
      </header>
      <div className="stack-list">
        {stack.length === 0 && <span className="empty-hint">当前无结算链项目</span>}
        {stack.map((item, index) => (
          <article className="stack-item" key={index}>
            <strong>项目 {stack.length - index}</strong>
            <code>{JSON.stringify(item)}</code>
          </article>
        ))}
      </div>
      <div className="task-queue">
        <strong>待处理任务</strong>
        <span>阶段：{asString(queue.phase, "无")}</span>
        <span>活动任务：{asString(queue.activeTaskId, "无")}</span>
        <span>{queue.isBlocking ? "阻塞普通行动" : "不阻塞"}</span>
      </div>
    </section>
  );
}
