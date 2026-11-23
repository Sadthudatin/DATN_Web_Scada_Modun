import aedes from 'aedes';
import net from 'node:net';

const port = Number(process.env.MQTT_PORT || 1884);
const broker = aedes();
const server = net.createServer(broker.handle);
server.listen(port, () => console.log(`[BROKER] listening on ${port} (dev only)`));
broker.on('client', client => console.log(`[BROKER] connected ${client.id}`));
