import { useEffect, useRef } from 'react';
import {
  CandlestickSeries,
  ColorType,
  HistogramSeries,
  createChart,
  createSeriesMarkers,
} from 'lightweight-charts';
import {
  SESSION_VOLUME_COLORS,
  findBarTime,
  formatEasternTime,
  toChartTime,
} from '../utils/chartTime';

function buildMarkers(bars, trade, hindsight) {
  const markers = [];

  const entryTime = findBarTime(bars, trade?.entryTime);
  if (trade?.taken && entryTime) {
    markers.push({
      time: entryTime,
      position: 'belowBar',
      color: '#38bdf8',
      shape: 'arrowUp',
      text: 'Buy',
    });
  }

  const exitTime = findBarTime(bars, trade?.exitTime);
  if (trade?.taken && exitTime) {
    markers.push({
      time: exitTime,
      position: 'aboveBar',
      color: '#fbbf24',
      shape: 'arrowDown',
      text: trade.exitReason ?? 'Exit',
    });
  }

  const buyTime = findBarTime(bars, hindsight?.buyTime);
  if (buyTime) {
    markers.push({
      time: buyTime,
      position: 'belowBar',
      color: '#a78bfa',
      shape: 'circle',
      text: 'Ideal buy',
    });
  }

  const sellTime = findBarTime(bars, hindsight?.sellTime);
  if (sellTime) {
    markers.push({
      time: sellTime,
      position: 'aboveBar',
      color: '#a78bfa',
      shape: 'circle',
      text: 'Ideal sell',
    });
  }

  return markers.sort((a, b) => a.time - b.time);
}

export default function BacktestChart({ bars, trade, hindsight }) {
  const containerRef = useRef(null);
  const chartRef = useRef(null);

  useEffect(() => {
    if (!containerRef.current || !bars?.length) {
      return undefined;
    }

    const chart = createChart(containerRef.current, {
      layout: {
        background: { type: ColorType.Solid, color: '#020617' },
        textColor: '#cbd5e1',
      },
      grid: {
        vertLines: { color: '#1e293b' },
        horzLines: { color: '#1e293b' },
      },
      rightPriceScale: {
        borderColor: '#334155',
      },
      timeScale: {
        borderColor: '#334155',
        timeVisible: true,
        secondsVisible: false,
      },
      localization: {
        timeFormatter: (time) => formatEasternTime(new Date(time * 1000).toISOString()),
      },
    });

    const candleSeries = chart.addSeries(CandlestickSeries, {
      upColor: '#22c55e',
      downColor: '#ef4444',
      borderVisible: false,
      wickUpColor: '#22c55e',
      wickDownColor: '#ef4444',
    });

    const volumeSeries = chart.addSeries(HistogramSeries, {
      priceFormat: { type: 'volume' },
      priceScaleId: 'volume',
    });

    chart.priceScale('volume').applyOptions({
      scaleMargins: { top: 0.8, bottom: 0 },
    });

    const candleData = bars.map((bar) => ({
      time: toChartTime(bar.t),
      open: bar.o,
      high: bar.h,
      low: bar.l,
      close: bar.c,
    }));

    const volumeData = bars.map((bar) => ({
      time: toChartTime(bar.t),
      value: bar.v,
      color: SESSION_VOLUME_COLORS[bar.session] ?? SESSION_VOLUME_COLORS.regular,
    }));

    candleSeries.setData(candleData);
    volumeSeries.setData(volumeData);

    const markers = buildMarkers(bars, trade, hindsight);
    if (markers.length > 0) {
      createSeriesMarkers(candleSeries, markers);
    }

    chart.timeScale().fitContent();

    const resizeObserver = new ResizeObserver((entries) => {
      const { width, height } = entries[0].contentRect;
      chart.applyOptions({ width, height });
    });
    resizeObserver.observe(containerRef.current);

    chartRef.current = chart;

    return () => {
      resizeObserver.disconnect();
      chart.remove();
      chartRef.current = null;
    };
  }, [bars, trade, hindsight]);

  if (!bars?.length) {
    return (
      <div className="flex h-96 items-center justify-center rounded-lg border border-dashed border-slate-700 text-slate-400">
        Run a backtest to render the chart.
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap gap-4 text-xs text-slate-400">
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-2.5 w-2.5 rounded-sm bg-violet-500/50" />
          Premarket volume
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-2.5 w-2.5 rounded-sm bg-slate-500/60" />
          Regular session
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-2.5 w-2.5 rounded-sm bg-sky-400/50" />
          Post-market volume
        </span>
        <span className="text-slate-500">Time axis shown in US/Eastern</span>
      </div>
      <div ref={containerRef} className="h-[28rem] w-full rounded-lg border border-slate-700" />
    </div>
  );
}
