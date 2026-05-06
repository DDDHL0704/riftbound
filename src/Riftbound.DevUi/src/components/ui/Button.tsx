import { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  icon?: ReactNode;
  variant?: "primary" | "secondary" | "ghost" | "danger";
};

export function Button({ children, icon, variant = "primary", className = "", ...props }: ButtonProps) {
  return (
    <button className={`button button-${variant} ${className}`.trim()} type="button" {...props}>
      {icon}
      <span>{children}</span>
    </button>
  );
}
