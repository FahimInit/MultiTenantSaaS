"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

/* -------------------------------------------------------------------------- */
/* 1. Semi-Circle Health Score Gauge (0-100 with needle & ticks)              */
/* -------------------------------------------------------------------------- */

export interface HealthGaugeProps {
  score: number;
  label?: string;
  className?: string;
}

export function HealthGauge({ score, label, className }: HealthGaugeProps) {
  const clamped = Math.max(0, Math.min(100, Math.round(score)));
  const rating =
    label ??
    (clamped >= 85
      ? "Excellent"
      : clamped >= 70
        ? "Good"
        : clamped >= 50
          ? "Fair"
          : "At Risk");

  const accentColor =
    clamped >= 70
      ? "#10b981" // emerald-500
      : clamped >= 50
        ? "#f59e0b" // amber-500
        : "#ef4444"; // red-500

  const cx = 120;
  const cy = 112;
  const radius = 82;
  const circumference = Math.PI * radius;
  const dashOffset = circumference * (1 - clamped / 100);

  // Needle angle: 0 -> -180deg (left), 100 -> 0deg (right)
  const angleRad = Math.PI * (1 - clamped / 100);
  const needleLen = 68;
  const needleX = cx + needleLen * Math.cos(angleRad);
  const needleY = cy - needleLen * Math.sin(angleRad);

  const ticks = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];

  return (
    <div className={cn("flex flex-col items-center", className)}>
      <svg
        viewBox="0 0 240 136"
        className="w-full max-w-[230px] overflow-visible"
        role="img"
        aria-label={`Health score ${clamped} out of 100 (${rating})`}
      >
        {/* Background track */}
        <path
          d={`M ${cx - radius} ${cy} A ${radius} ${radius} 0 0 1 ${cx + radius} ${cy}`}
          fill="none"
          stroke="currentColor"
          strokeWidth="16"
          strokeLinecap="round"
          className="text-muted/70"
        />

        {/* Active value arc */}
        <path
          d={`M ${cx - radius} ${cy} A ${radius} ${radius} 0 0 1 ${cx + radius} ${cy}`}
          fill="none"
          stroke={accentColor}
          strokeWidth="16"
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={dashOffset}
          className="transition-all duration-700 ease-out"
        />

        {/* Scale numbers around outer arc */}
        {ticks.map((t) => {
          const a = Math.PI * (1 - t / 100);
          const tx = cx + (radius + 17) * Math.cos(a);
          const ty = cy - (radius + 17) * Math.sin(a);
          return (
            <text
              key={t}
              x={tx}
              y={ty + 3}
              textAnchor="middle"
              className="fill-muted-foreground text-[8.5px] font-medium"
            >
              {t}
            </text>
          );
        })}

        {/* Pointer needle */}
        <line
          x1={cx}
          y1={cy}
          x2={needleX}
          y2={needleY}
          stroke="currentColor"
          strokeWidth="3"
          strokeLinecap="round"
          className="text-foreground/75 transition-all duration-700 ease-out"
        />
        <circle
          cx={needleX}
          cy={needleY}
          r="3.5"
          fill="#ffffff"
          stroke={accentColor}
          strokeWidth="2.5"
        />
        <circle cx={cx} cy={cy} r="4.5" className="fill-muted-foreground/40" />

        {/* Center score & status */}
        <text
          x={cx}
          y={cy - 18}
          textAnchor="middle"
          fill={accentColor}
          className="text-[30px] font-bold tracking-tight"
        >
          {clamped}
        </text>
        <text
          x={cx}
          y={cy - 1}
          textAnchor="middle"
          fill={accentColor}
          className="text-[12px] font-semibold"
        >
          {rating}
        </text>
      </svg>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* 2. Grouped Bar Chart (Navy vs Orange bars with interactive dark tooltip)   */
/* -------------------------------------------------------------------------- */

export interface GroupedBarPoint {
  label: string;
  primary: number;
  secondary: number;
}

export interface GroupedBarChartProps {
  data: GroupedBarPoint[];
  primaryLabel: string;
  secondaryLabel: string;
  primaryColor?: string;
  secondaryColor?: string;
  valueFormatter?: (v: number) => string;
  className?: string;
}

export function GroupedBarChart({
  data,
  primaryLabel,
  secondaryLabel,
  primaryColor = "#1e3a8a",
  secondaryColor = "#f97316",
  valueFormatter = (v) => v.toLocaleString(),
  className,
}: GroupedBarChartProps) {
  const [hoveredIdx, setHoveredIdx] = React.useState<number | null>(null);

  const maxRaw = Math.max(
    1,
    ...data.flatMap((d) => [d.primary, d.secondary])
  );
  // Nice rounded upper bound
  const magnitude = Math.pow(10, Math.floor(Math.log10(maxRaw)));
  const maxVal = Math.ceil(maxRaw / magnitude) * magnitude;

  const width = 460;
  const height = 210;
  const padLeft = 42;
  const padRight = 12;
  const padTop = 16;
  const padBottom = 30;
  const plotW = width - padLeft - padRight;
  const plotH = height - padTop - padBottom;

  const yTicks = [0, 0.25, 0.5, 0.75, 1];
  const groupW = plotW / Math.max(1, data.length);
  const barW = Math.min(13, Math.max(6, groupW * 0.28));
  const gap = 3;

  const formatAxis = (v: number) => {
    if (v >= 1000000) return `${(v / 1000000).toFixed(1)}M`;
    if (v >= 1000) return `${(v / 1000).toFixed(v % 1000 === 0 ? 0 : 1)}k`;
    return String(Math.round(v));
  };

  return (
    <div className={cn("w-full", className)}>
      <div className="relative w-full">
        <svg
          viewBox={`0 0 ${width} ${height}`}
          className="h-auto w-full overflow-visible"
          role="img"
          aria-label={`${primaryLabel} vs ${secondaryLabel} bar chart`}
        >
          {/* Horizontal grid lines */}
          {yTicks.map((ratio) => {
            const val = maxVal * ratio;
            const y = padTop + plotH * (1 - ratio);
            return (
              <g key={ratio}>
                <line
                  x1={padLeft}
                  y1={y}
                  x2={width - padRight}
                  y2={y}
                  stroke="currentColor"
                  strokeDasharray={ratio === 0 ? undefined : "3 3"}
                  className="text-border"
                  strokeWidth="1"
                />
                <text
                  x={padLeft - 8}
                  y={y + 3}
                  textAnchor="end"
                  className="fill-muted-foreground text-[10px]"
                >
                  {formatAxis(val)}
                </text>
              </g>
            );
          })}

          {/* Bar groups */}
          {data.map((point, idx) => {
            const centerX = padLeft + idx * groupW + groupW / 2;
            const h1 = (point.primary / maxVal) * plotH;
            const h2 = (point.secondary / maxVal) * plotH;
            const isHovered = hoveredIdx === idx;

            return (
              <g
                key={point.label}
                onMouseEnter={() => setHoveredIdx(idx)}
                onMouseLeave={() => setHoveredIdx(null)}
                className="cursor-pointer"
              >
                {/* Hover highlight column */}
                <rect
                  x={padLeft + idx * groupW + 2}
                  y={padTop}
                  width={Math.max(0, groupW - 4)}
                  height={plotH}
                  rx="4"
                  className={cn(
                    "transition-opacity duration-150",
                    isHovered ? "fill-muted/60 opacity-100" : "opacity-0"
                  )}
                />

                {/* Primary bar */}
                <rect
                  x={centerX - barW - gap / 2}
                  y={padTop + plotH - h1}
                  width={barW}
                  height={Math.max(2, h1)}
                  rx="2.5"
                  fill={primaryColor}
                  className="transition-all duration-300"
                />

                {/* Secondary bar */}
                <rect
                  x={centerX + gap / 2}
                  y={padTop + plotH - h2}
                  width={barW}
                  height={Math.max(2, h2)}
                  rx="2.5"
                  fill={secondaryColor}
                  className="transition-all duration-300"
                />

                {/* X-axis label */}
                <text
                  x={centerX}
                  y={height - 10}
                  textAnchor="middle"
                  className={cn(
                    "text-[10px]",
                    isHovered
                      ? "fill-foreground font-semibold"
                      : "fill-muted-foreground"
                  )}
                >
                  {point.label}
                </text>
              </g>
            );
          })}
        </svg>

        {/* Floating dark tooltip */}
        {hoveredIdx !== null && data[hoveredIdx] && (
          <div
            className="pointer-events-none absolute top-1 z-20 -translate-x-1/2 rounded-md bg-slate-900 px-3 py-2 text-[11px] text-white shadow-lg dark:bg-slate-800 dark:ring-1 dark:ring-slate-700"
            style={{
              left: `${Math.min(
                84,
                Math.max(
                  16,
                  ((padLeft + hoveredIdx * groupW + groupW / 2) / width) * 100
                )
              )}%`,
            }}
          >
            <p className="mb-1 font-semibold text-slate-200">
              {data[hoveredIdx].label}
            </p>
            <div className="flex items-center gap-2 whitespace-nowrap">
              <span
                className="h-2 w-2 rounded-sm"
                style={{ backgroundColor: primaryColor }}
              />
              <span className="text-slate-300">{primaryLabel}:</span>
              <span className="font-mono font-semibold">
                {valueFormatter(data[hoveredIdx].primary)}
              </span>
            </div>
            <div className="mt-0.5 flex items-center gap-2 whitespace-nowrap">
              <span
                className="h-2 w-2 rounded-sm"
                style={{ backgroundColor: secondaryColor }}
              />
              <span className="text-slate-300">{secondaryLabel}:</span>
              <span className="font-mono font-semibold">
                {valueFormatter(data[hoveredIdx].secondary)}
              </span>
            </div>
          </div>
        )}
      </div>

      {/* Legend */}
      <div className="mt-2 flex items-center justify-center gap-5 text-xs text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <span
            className="h-2.5 w-2.5 rounded-sm"
            style={{ backgroundColor: primaryColor }}
          />
          {primaryLabel}
        </span>
        <span className="flex items-center gap-1.5">
          <span
            className="h-2.5 w-2.5 rounded-sm"
            style={{ backgroundColor: secondaryColor }}
          />
          {secondaryLabel}
        </span>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* 3. Donut / Ring Chart (Colorful slices + side legend + hover tooltip)      */
/* -------------------------------------------------------------------------- */

export interface DonutSlice {
  label: string;
  value: number;
  color: string;
}

export interface DonutChartProps {
  slices: DonutSlice[];
  centerValue?: string;
  centerLabel?: string;
  valueFormatter?: (v: number) => string;
  className?: string;
}

export function DonutChart({
  slices,
  centerValue,
  centerLabel,
  valueFormatter = (v) => v.toLocaleString(),
  className,
}: DonutChartProps) {
  const [hoveredIdx, setHoveredIdx] = React.useState<number | null>(null);

  const nonZero = slices.filter((s) => s.value > 0);
  const total = nonZero.reduce((sum, s) => sum + s.value, 0);

  const radius = 60;
  const strokeWidth = 26;
  const circumference = 2 * Math.PI * radius;

  let cumulative = 0;
  const segments = nonZero.map((slice) => {
    const fraction = total > 0 ? slice.value / total : 0;
    const length = fraction * circumference;
    const offset = -cumulative;
    cumulative += length;
    return {
      ...slice,
      fraction,
      length,
      offset,
    };
  });

  const activeSlice =
    hoveredIdx !== null && segments[hoveredIdx] ? segments[hoveredIdx] : null;

  return (
    <div
      className={cn(
        "flex flex-col items-center gap-4 sm:flex-row sm:items-center sm:justify-between",
        className
      )}
    >
      <div className="relative flex shrink-0 items-center justify-center">
        <svg
          viewBox="0 0 164 164"
          className="h-40 w-40 -rotate-90"
          role="img"
          aria-label="Distribution donut chart"
        >
          {total === 0 ? (
            <circle
              cx="82"
              cy="82"
              r={radius}
              fill="none"
              stroke="currentColor"
              strokeWidth={strokeWidth}
              className="text-muted/60"
            />
          ) : (
            segments.map((seg, idx) => {
              const isHovered = hoveredIdx === idx;
              return (
                <circle
                  key={seg.label}
                  cx="82"
                  cy="82"
                  r={radius}
                  fill="none"
                  stroke={seg.color}
                  strokeWidth={isHovered ? strokeWidth + 4 : strokeWidth}
                  strokeDasharray={`${Math.max(0, seg.length - 1.5)} ${circumference}`}
                  strokeDashoffset={seg.offset}
                  onMouseEnter={() => setHoveredIdx(idx)}
                  onMouseLeave={() => setHoveredIdx(null)}
                  className="cursor-pointer transition-all duration-200"
                />
              );
            })
          )}
        </svg>

        {/* Center label */}
        <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center text-center">
          <span className="text-xl font-bold leading-none">
            {activeSlice
              ? valueFormatter(activeSlice.value)
              : (centerValue ?? valueFormatter(total))}
          </span>
          <span className="mt-1 max-w-[76px] truncate text-[10px] font-medium text-muted-foreground">
            {activeSlice ? activeSlice.label : (centerLabel ?? "Total")}
          </span>
        </div>

        {/* Floating dark badge on hover */}
        {activeSlice && (
          <div className="pointer-events-none absolute -top-2 left-1/2 z-20 -translate-x-1/2 whitespace-nowrap rounded-md bg-slate-900 px-2.5 py-1 text-[11px] text-white shadow-lg dark:bg-slate-800 dark:ring-1 dark:ring-slate-700">
            <span className="font-semibold">{activeSlice.label}:</span>{" "}
            <span className="font-mono">
              {valueFormatter(activeSlice.value)} (
              {Math.round(activeSlice.fraction * 100)}%)
            </span>
          </div>
        )}
      </div>

      {/* Legend */}
      <div className="w-full min-w-0 flex-1 space-y-2">
        {slices.map((slice) => {
          const segIdx = segments.findIndex((s) => s.label === slice.label);
          const pct =
            total > 0 ? Math.round((slice.value / total) * 100) : 0;
          const isHovered = segIdx !== -1 && hoveredIdx === segIdx;
          return (
            <div
              key={slice.label}
              onMouseEnter={() => segIdx !== -1 && setHoveredIdx(segIdx)}
              onMouseLeave={() => setHoveredIdx(null)}
              className={cn(
                "flex cursor-default items-center justify-between gap-2 rounded px-1.5 py-0.5 text-xs transition-colors",
                isHovered && "bg-muted/70"
              )}
            >
              <span className="flex min-w-0 items-center gap-2">
                <span
                  className="h-2.5 w-3.5 shrink-0 rounded-sm"
                  style={{ backgroundColor: slice.color }}
                />
                <span className="truncate text-muted-foreground">
                  {slice.label}
                </span>
              </span>
              <span className="shrink-0 font-mono text-[11px] font-medium text-foreground">
                {valueFormatter(slice.value)}{" "}
                <span className="text-muted-foreground">({pct}%)</span>
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* 4. Segmented Horizontal Bar (Like All Receivables / All Payables)          */
/* -------------------------------------------------------------------------- */

export interface SegmentedQuotaBarProps {
  title: string;
  primaryLabel: string;
  primaryValue: number;
  secondaryLabel: string;
  secondaryValue: number;
  primaryColor?: string;
  secondaryColor?: string;
  subtitleLeft?: string;
  subtitleRight?: string;
  valueFormatter?: (v: number) => string;
}

export function SegmentedQuotaBar({
  title,
  primaryLabel,
  primaryValue,
  secondaryLabel,
  secondaryValue,
  primaryColor = "#1e3a8a",
  secondaryColor = "#f97316",
  subtitleLeft,
  subtitleRight,
  valueFormatter = (v) => v.toLocaleString(),
}: SegmentedQuotaBarProps) {
  const total = Math.max(1, primaryValue + secondaryValue);
  const primaryPct = Math.round((primaryValue / total) * 100);
  const secondaryPct = 100 - primaryPct;

  return (
    <div className="space-y-2.5">
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-semibold">{title}</h4>
        <span className="font-mono text-xs text-muted-foreground">
          {primaryPct}% used
        </span>
      </div>

      {/* Segmented Bar */}
      <div className="flex h-6 w-full overflow-hidden rounded-md bg-muted/60">
        {primaryPct > 0 && (
          <div
            style={{
              width: `${primaryPct}%`,
              backgroundColor: primaryColor,
            }}
            className="h-full transition-all duration-500"
            title={`${primaryLabel}: ${valueFormatter(primaryValue)}`}
          />
        )}
        {secondaryPct > 0 && (
          <div
            style={{
              width: `${secondaryPct}%`,
              backgroundColor: secondaryColor,
            }}
            className="h-full transition-all duration-500"
            title={`${secondaryLabel}: ${valueFormatter(secondaryValue)}`}
          />
        )}
      </div>

      {/* Legend */}
      <div className="flex items-center justify-center gap-4 text-[11px] text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <span
            className="h-2 w-2 rounded-sm"
            style={{ backgroundColor: primaryColor }}
          />
          {primaryLabel}
        </span>
        <span className="flex items-center gap-1.5">
          <span
            className="h-2 w-2 rounded-sm"
            style={{ backgroundColor: secondaryColor }}
          />
          {secondaryLabel}
        </span>
      </div>

      {/* Detail grid */}
      <div className="grid grid-cols-2 gap-3 border-t pt-2.5 text-xs">
        <div>
          <p className="text-[11px] text-muted-foreground">{primaryLabel}</p>
          <p className="font-mono text-sm font-semibold text-foreground">
            {valueFormatter(primaryValue)}
          </p>
          {subtitleLeft && (
            <p className="text-[11px] text-muted-foreground">{subtitleLeft}</p>
          )}
        </div>
        <div>
          <p className="text-[11px] text-muted-foreground">{secondaryLabel}</p>
          <p className="font-mono text-sm font-semibold text-foreground">
            {valueFormatter(secondaryValue)}
          </p>
          {subtitleRight && (
            <p className="text-[11px] text-muted-foreground">{subtitleRight}</p>
          )}
        </div>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* 5. Line Trend Chart (Dark navy line + orange dots + dark hover tooltip)    */
/* -------------------------------------------------------------------------- */

export interface LineTrendPoint {
  label: string;
  value: number;
}

export interface LineTrendChartProps {
  data: LineTrendPoint[];
  valueLabel: string;
  lineColor?: string;
  dotColor?: string;
  valueFormatter?: (v: number) => string;
  className?: string;
}

export function LineTrendChart({
  data,
  valueLabel,
  lineColor = "#1e3a8a",
  dotColor = "#f97316",
  valueFormatter = (v) => v.toLocaleString(),
  className,
}: LineTrendChartProps) {
  const [hoveredIdx, setHoveredIdx] = React.useState<number | null>(null);

  const maxRaw = Math.max(1, ...data.map((d) => d.value));
  const magnitude = Math.pow(10, Math.floor(Math.log10(maxRaw)));
  const maxVal = Math.ceil(maxRaw / magnitude) * magnitude;

  const width = 460;
  const height = 190;
  const padLeft = 42;
  const padRight = 16;
  const padTop = 16;
  const padBottom = 28;
  const plotW = width - padLeft - padRight;
  const plotH = height - padTop - padBottom;

  const yTicks = [0, 0.25, 0.5, 0.75, 1];
  const stepX = data.length > 1 ? plotW / (data.length - 1) : plotW;

  const points = data.map((d, i) => ({
    ...d,
    x: padLeft + i * stepX,
    y: padTop + plotH * (1 - d.value / maxVal),
  }));

  const pathD = points
    .map((p, i) => `${i === 0 ? "M" : "L"} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`)
    .join(" ");

  const areaD =
    points.length > 0
      ? `${pathD} L ${points[points.length - 1].x.toFixed(1)} ${(padTop + plotH).toFixed(1)} L ${points[0].x.toFixed(1)} ${(padTop + plotH).toFixed(1)} Z`
      : "";

  const formatAxis = (v: number) => {
    if (v >= 1000000) return `${(v / 1000000).toFixed(1)}M`;
    if (v >= 1000) return `${(v / 1000).toFixed(v % 1000 === 0 ? 0 : 1)}k`;
    return String(Math.round(v));
  };

  return (
    <div className={cn("relative w-full", className)}>
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="h-auto w-full overflow-visible"
        role="img"
        aria-label={`${valueLabel} trend line chart`}
      >
        <defs>
          <linearGradient id="trendAreaFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={lineColor} stopOpacity="0.16" />
            <stop offset="100%" stopColor={lineColor} stopOpacity="0.0" />
          </linearGradient>
        </defs>

        {/* Grid lines */}
        {yTicks.map((ratio) => {
          const val = maxVal * ratio;
          const y = padTop + plotH * (1 - ratio);
          return (
            <g key={ratio}>
              <line
                x1={padLeft}
                y1={y}
                x2={width - padRight}
                y2={y}
                stroke="currentColor"
                strokeDasharray={ratio === 0 ? undefined : "3 3"}
                className="text-border"
                strokeWidth="1"
              />
              <text
                x={padLeft - 8}
                y={y + 3}
                textAnchor="end"
                className="fill-muted-foreground text-[10px]"
              >
                {formatAxis(val)}
              </text>
            </g>
          );
        })}

        {/* Subtle area fill */}
        {areaD && <path d={areaD} fill="url(#trendAreaFill)" />}

        {/* Main polyline */}
        {pathD && (
          <path
            d={pathD}
            fill="none"
            stroke={lineColor}
            strokeWidth="2.25"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="dark:stroke-blue-400"
          />
        )}

        {/* Data points & X-axis labels */}
        {points.map((p, idx) => {
          const isHovered = hoveredIdx === idx;
          return (
            <g
              key={p.label}
              onMouseEnter={() => setHoveredIdx(idx)}
              onMouseLeave={() => setHoveredIdx(null)}
              className="cursor-pointer"
            >
              {/* Invisible wider hit area */}
              <rect
                x={p.x - Math.max(12, stepX / 2)}
                y={padTop}
                width={Math.max(24, stepX)}
                height={plotH + padBottom}
                fill="transparent"
              />
              {isHovered && (
                <line
                  x1={p.x}
                  y1={padTop}
                  x2={p.x}
                  y2={padTop + plotH}
                  stroke={dotColor}
                  strokeDasharray="3 3"
                  strokeWidth="1"
                  opacity="0.7"
                />
              )}
              <circle
                cx={p.x}
                cy={p.y}
                r={isHovered ? 5.5 : 3.75}
                fill={dotColor}
                stroke="#ffffff"
                strokeWidth="1.75"
                className="transition-all duration-150"
              />
              <text
                x={p.x}
                y={height - 8}
                textAnchor="middle"
                className={cn(
                  "text-[10px]",
                  isHovered
                    ? "fill-foreground font-semibold"
                    : "fill-muted-foreground"
                )}
              >
                {p.label}
              </text>
            </g>
          );
        })}
      </svg>

      {/* Floating dark tooltip */}
      {hoveredIdx !== null && points[hoveredIdx] && (
        <div
          className="pointer-events-none absolute top-1 z-20 -translate-x-1/2 whitespace-nowrap rounded-md bg-slate-900 px-2.5 py-1.5 text-[11px] text-white shadow-lg dark:bg-slate-800 dark:ring-1 dark:ring-slate-700"
          style={{
            left: `${Math.min(
              84,
              Math.max(16, (points[hoveredIdx].x / width) * 100)
            )}%`,
          }}
        >
          <span className="font-semibold text-slate-300">
            {points[hoveredIdx].label}:
          </span>{" "}
          <span className="font-mono font-semibold">
            {valueFormatter(points[hoveredIdx].value)}
          </span>{" "}
          <span className="text-slate-400">{valueLabel}</span>
        </div>
      )}
    </div>
  );
}
