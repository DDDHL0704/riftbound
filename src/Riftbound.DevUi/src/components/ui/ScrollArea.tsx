import * as ScrollAreaPrimitive from "@radix-ui/react-scroll-area";
import type { ComponentPropsWithoutRef } from "react";

type ScrollAreaProps = ComponentPropsWithoutRef<typeof ScrollAreaPrimitive.Root> & {
  viewportClassName?: string;
};

export function ScrollArea({ children, className = "", viewportClassName = "", ...props }: ScrollAreaProps) {
  return (
    <ScrollAreaPrimitive.Root className={`scroll-area ${className}`.trim()} {...props}>
      <ScrollAreaPrimitive.Viewport className={`scroll-area-viewport ${viewportClassName}`.trim()} tabIndex={0}>
        {children}
      </ScrollAreaPrimitive.Viewport>
      <ScrollAreaPrimitive.Scrollbar className="scroll-area-scrollbar" orientation="vertical">
        <ScrollAreaPrimitive.Thumb className="scroll-area-thumb" />
      </ScrollAreaPrimitive.Scrollbar>
      <ScrollAreaPrimitive.Scrollbar className="scroll-area-scrollbar scroll-area-scrollbar-horizontal" orientation="horizontal">
        <ScrollAreaPrimitive.Thumb className="scroll-area-thumb" />
      </ScrollAreaPrimitive.Scrollbar>
      <ScrollAreaPrimitive.Corner className="scroll-area-corner" />
    </ScrollAreaPrimitive.Root>
  );
}
