import { useEffect, useState } from 'react';

/**
 * Green Pac-Man style pie: thin wedge grows into a full disc at 100 percent.
 */
export default function PacManProgress({
  percent = 0,
  size = 44,
  label,
  animate = true,
}) {
  const [displayPercent, setDisplayPercent] = useState(percent);
  const clamped = Math.min(100, Math.max(0, displayPercent));
  const reducedMotion = usePrefersReducedMotion();

  useEffect(() => {
    if (reducedMotion || !animate) {
      setDisplayPercent(percent);
      return undefined;
    }

    const timer = window.setTimeout(() => setDisplayPercent(percent), 80);
    return () => window.clearTimeout(timer);
  }, [percent, reducedMotion, animate]);

  const radius = size / 2;
  const center = radius;
  const angle = (clamped / 100) * 360;
  const path = describeWedge(center, center, radius - 2, angle);

  return (
    <div className="flex flex-col items-center gap-1" role="img" aria-label={label ?? `Coverage ${clamped} percent`}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} className="shrink-0">
        <circle cx={center} cy={center} r={radius - 2} fill="rgba(15, 23, 42, 0.6)" />
        {clamped > 0 && (
          <path
            d={path}
            fill="rgb(34, 197, 94)"
            style={{
              transition: reducedMotion || !animate ? 'none' : 'd 300ms ease-out',
            }}
          />
        )}
        {clamped >= 100 && (
          <circle cx={center} cy={center} r={radius - 2} fill="rgb(34, 197, 94)" />
        )}
      </svg>
      <span className="text-[10px] text-slate-500">{clamped}%</span>
    </div>
  );
}

function describeWedge(cx, cy, r, sweepDegrees) {
  if (sweepDegrees <= 0) {
    return '';
  }

  if (sweepDegrees >= 360) {
    return `M ${cx} ${cy} m -${r}, 0 a ${r},${r} 0 1,0 ${r * 2},0 a ${r},${r} 0 1,0 -${r * 2},0`;
  }

  const startAngle = -90;
  const endAngle = startAngle + sweepDegrees;
  const start = polar(cx, cy, r, startAngle);
  const end = polar(cx, cy, r, endAngle);
  const largeArc = sweepDegrees > 180 ? 1 : 0;
  return `M ${cx} ${cy} L ${start.x} ${start.y} A ${r} ${r} 0 ${largeArc} 1 ${end.x} ${end.y} Z`;
}

function polar(cx, cy, r, degrees) {
  const radians = (degrees * Math.PI) / 180;
  return {
    x: cx + r * Math.cos(radians),
    y: cy + r * Math.sin(radians),
  };
}

function usePrefersReducedMotion() {
  const [reduced, setReduced] = useState(false);

  useEffect(() => {
    const media = window.matchMedia('(prefers-reduced-motion: reduce)');
    setReduced(media.matches);
    function onChange() {
      setReduced(media.matches);
    }

    media.addEventListener('change', onChange);
    return () => media.removeEventListener('change', onChange);
  }, []);

  return reduced;
}
