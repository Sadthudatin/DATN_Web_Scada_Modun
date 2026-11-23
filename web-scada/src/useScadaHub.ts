import { useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import type { Alarm, Command, GatewayStatusEvent, LiveTelemetry } from './types';

interface HubHandlers {
  onTelemetry?: (event: LiveTelemetry) => void;
  onGatewayStatus?: (event: GatewayStatusEvent) => void;
  onAlarm?: (event: Alarm) => void;
  onCommandStatus?: (event: Command) => void;
}

export function useScadaHub(token: string | null, handlers: HubHandlers) {
  const handlersRef = useRef(handlers);
  handlersRef.current = handlers;
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    if (!token) return;
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/scada', { accessTokenFactory: () => token })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    connection.on('telemetry', (event: LiveTelemetry) => handlersRef.current.onTelemetry?.(event));
    connection.on('gatewayStatus', (event: GatewayStatusEvent) => handlersRef.current.onGatewayStatus?.(event));
    connection.on('alarm', (event: Alarm) => handlersRef.current.onAlarm?.(event));
    connection.on('commandStatus', (event: Command) => handlersRef.current.onCommandStatus?.(event));
    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    let disposed = false;
    const startPromise = connection.start()
      .then(() => { if (!disposed) setConnected(true); })
      .catch(() => { if (!disposed) setConnected(false); });
    return () => {
      disposed = true;
      setConnected(false);
      void startPromise.finally(() => connection.stop().catch(() => undefined));
    };
  }, [token]);

  return connected;
}
