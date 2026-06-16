import { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  icon?: ReactNode;
  variant?: "primary" | "secondary" | "ghost" | "danger";
};

export function Button({ children, icon, variant = "primary", className = "", type = "button", ...props }: ButtonProps) {
  const ariaLabel = props["aria-label"] ?? (typeof children === "string" ? children : undefined);

  return (
    <button className={`button button-${variant} ${className}`.trim()} type={type} {...props} aria-label={ariaLabel}>
      {icon}
      <span>{children}</span>
    </button>
  );
}
