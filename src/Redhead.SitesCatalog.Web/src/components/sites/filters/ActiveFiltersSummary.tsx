import { useLayoutEffect, useRef, useState } from 'react';
import { Box } from '@mui/material';
import {
  formatFilterGroupOverflow,
  type ActiveFilterSummary,
} from './ActiveFiltersSummary.helpers';

interface SummaryItemProps {
  item: ActiveFilterSummary;
  showSeparator: boolean;
}

function ActiveFilterSummaryItem({ item, showSeparator }: SummaryItemProps) {
  return (
    <Box
      component="span"
      data-summary-item
      sx={{
        display: 'inline-flex',
        alignItems: 'center',
        minWidth: 0,
        whiteSpace: 'nowrap',
      }}
    >
      {showSeparator && (
        <Box
          component="span"
          aria-hidden="true"
          sx={{
            mx: 1,
            color: 'divider',
            fontWeight: 500,
            flexShrink: 0,
          }}
        >
          |
        </Box>
      )}
      <Box
        component="span"
        sx={{
          display: 'inline-flex',
          alignItems: 'baseline',
          minWidth: 0,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
        }}
      >
        {item.label && (
          <Box
            component="span"
            sx={{ color: 'text.secondary', fontWeight: 500, flexShrink: 0 }}
          >
            {item.label}:&nbsp;
          </Box>
        )}
        <Box
          component="span"
          sx={{
            minWidth: 0,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            color: 'text.secondary',
          }}
        >
          {item.value}
        </Box>
      </Box>
    </Box>
  );
}

export function ActiveFiltersSummary({ items }: Readonly<{ items: ActiveFilterSummary[] }>) {
  const containerRef = useRef<HTMLSpanElement | null>(null);
  const measureRef = useRef<HTMLSpanElement | null>(null);
  const overflowWithSeparatorRef = useRef<HTMLSpanElement | null>(null);
  const [visibleCount, setVisibleCount] = useState(items.length);

  useLayoutEffect(() => {
    const container = containerRef.current;
    const measure = measureRef.current;
    if (!container || !measure) return;

    const recalculate = () => {
      const availableWidth = container.clientWidth;
      if (availableWidth <= 0) return;

      const itemNodes = Array.from(
        measure.querySelectorAll<HTMLElement>('[data-summary-item]')
      );
      const overflowWithSeparatorWidth = overflowWithSeparatorRef.current?.offsetWidth ?? 0;
      let usedWidth = 0;
      let nextVisibleCount = 0;

      for (let index = 0; index < itemNodes.length; index += 1) {
        const hiddenAfterThisItem = items.length - index - 1;
        const overflowWidth = hiddenAfterThisItem > 0 ? overflowWithSeparatorWidth : 0;
        const nextWidth = usedWidth + itemNodes[index].offsetWidth + overflowWidth;

        if (nextWidth > availableWidth) break;

        usedWidth += itemNodes[index].offsetWidth;
        nextVisibleCount = index + 1;
      }

      setVisibleCount((current) => (current === nextVisibleCount ? current : nextVisibleCount));
    };

    recalculate();

    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', recalculate);
      return () => window.removeEventListener('resize', recalculate);
    }

    const observer = new ResizeObserver(recalculate);
    observer.observe(container);
    observer.observe(measure);

    return () => observer.disconnect();
  }, [items]);

  const visibleItems = items.slice(0, visibleCount);
  const hiddenCount = items.length - visibleItems.length;
  const renderedItems =
    hiddenCount > 0 ? [...visibleItems, formatFilterGroupOverflow(hiddenCount)] : visibleItems;

  return (
    <Box
      component="span"
      ref={containerRef}
      sx={{
        flex: '1 1 auto',
        minWidth: 0,
        overflow: 'hidden',
        whiteSpace: 'nowrap',
        display: 'flex',
        alignItems: 'center',
        color: 'text.secondary',
        typography: 'body2',
        position: 'relative',
      }}
    >
      {renderedItems.map((item, index) => (
        <Box
          component="span"
          key={`${item.label ?? 'overflow'}-${item.value}-${index}`}
          sx={{
            display: 'inline-flex',
            minWidth: 0,
            flexShrink: index === renderedItems.length - 1 ? 1 : 0,
            overflow: 'hidden',
          }}
        >
          <ActiveFilterSummaryItem item={item} showSeparator={index > 0} />
        </Box>
      ))}

      <Box
        component="span"
        ref={measureRef}
        aria-hidden="true"
        sx={{
          position: 'absolute',
          visibility: 'hidden',
          pointerEvents: 'none',
          height: 0,
          overflow: 'hidden',
          whiteSpace: 'nowrap',
        }}
      >
        {items.map((item, index) => (
          <ActiveFilterSummaryItem
            key={`${item.label ?? 'item'}-${item.value}-${index}`}
            item={item}
            showSeparator={index > 0}
          />
        ))}
      </Box>
      <Box
        component="span"
        ref={overflowWithSeparatorRef}
        aria-hidden="true"
        sx={{
          position: 'absolute',
          visibility: 'hidden',
          pointerEvents: 'none',
          height: 0,
          overflow: 'hidden',
          whiteSpace: 'nowrap',
        }}
      >
        <ActiveFilterSummaryItem item={formatFilterGroupOverflow(items.length)} showSeparator />
      </Box>
    </Box>
  );
}
