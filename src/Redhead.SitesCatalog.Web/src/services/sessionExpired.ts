export interface SessionExpiredRoute {
  pathname: string;
  search: string;
  hash: string;
}

export interface SessionExpiredEvent {
  from: SessionExpiredRoute;
  code?: string;
  message?: string;
}

type SessionExpiredHandler = (event: SessionExpiredEvent) => void;

let sessionExpiredHandler: SessionExpiredHandler | null = null;

export function registerSessionExpiredHandler(handler: SessionExpiredHandler): () => void {
  sessionExpiredHandler = handler;

  return () => {
    if (sessionExpiredHandler === handler) {
      sessionExpiredHandler = null;
    }
  };
}

export function notifySessionExpired(code?: string, message?: string): void {
  if (!sessionExpiredHandler || window.location.pathname === '/login') {
    return;
  }

  sessionExpiredHandler({
    from: {
      pathname: window.location.pathname,
      search: window.location.search,
      hash: window.location.hash,
    },
    code,
    message,
  });
}
