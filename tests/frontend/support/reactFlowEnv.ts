// React Flow measures the DOM; jsdom has no layout. These stubs give it enough to render nodes and
// edges (following https://reactflow.dev/learn/advanced-use/testing). Loaded once from setup.ts.

class ResizeObserverStub {
  constructor(private readonly callback: ResizeObserverCallback) {}

  observe(target: Element) {
    const { offsetWidth: width, offsetHeight: height } = target as HTMLElement;
    const contentRect = { x: 0, y: 0, top: 0, left: 0, right: width, bottom: height, width, height } as DOMRectReadOnly;
    this.callback(
      [{ target, contentRect, borderBoxSize: [{ inlineSize: width, blockSize: height }] } as unknown as ResizeObserverEntry],
      this,
    );
  }

  unobserve() {}

  disconnect() {}
}

class DOMMatrixReadOnlyStub {
  m22: number;

  constructor(transform?: string) {
    const scale = transform?.match(/scale\(([\d.]+)\)/)?.[1];
    this.m22 = scale !== undefined ? Number(scale) : 1;
  }
}

if (!('ResizeObserver' in globalThis)) {
  globalThis.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver;
}
if (!('DOMMatrixReadOnly' in globalThis)) {
  globalThis.DOMMatrixReadOnly = DOMMatrixReadOnlyStub as unknown as typeof DOMMatrixReadOnly;
}

// Nodes get their size from their inline style.
Object.defineProperties(HTMLElement.prototype, {
  offsetHeight: {
    configurable: true,
    get(this: HTMLElement) {
      return parseFloat(this.style.height) || 1;
    },
  },
  offsetWidth: {
    configurable: true,
    get(this: HTMLElement) {
      return parseFloat(this.style.width) || 1;
    },
  },
});

if (!('getBBox' in SVGElement.prototype)) {
  Object.defineProperty(SVGElement.prototype, 'getBBox', { configurable: true, value: () => ({ x: 0, y: 0, width: 0, height: 0 }) });
}
