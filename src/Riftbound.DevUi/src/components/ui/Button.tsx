import { ButtonHTMLAttributes, ReactNode, forwardRef } from "react";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  icon?: ReactNode;
  variant?: "primary" | "secondary" | "ghost" | "danger";
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button({ children, icon, variant = "primary", className = "", type = "button", ...props }, ref) {
  const ariaLabel = props["aria-label"] ?? (typeof children === "string" ? children : undefined);

  return (
    <button className={`button button-${variant} ${className}`.trim()} ref={ref} type={type} {...props} aria-label={ariaLabel}>
      {icon}
      <span>{children}</span>
    </button>
  );
});
