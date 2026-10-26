// Loopback-only static production build + real API proxy; no response mocks.
import http from 'node:http';
import {readFile} from 'node:fs/promises';
import {resolve,extname,sep} from 'node:path';
const [rootArg,portArg,targetArg]=process.argv.slice(2),root=resolve(rootArg),target=new URL(targetArg);
if(target.hostname!=='127.0.0.1')throw Error('Only ephemeral loopback API allowed');
const types={'.js':'text/javascript','.css':'text/css','.html':'text/html','.svg':'image/svg+xml','.png':'image/png'};
http.createServer(async(req,res)=>{
  if(req.url.startsWith('/api/')) {
    const upstream=http.request(new URL(req.url,target),{method:req.method,headers:{...req.headers,host:target.host}},r=>{if(res.destroyed){r.destroy();return;}res.writeHead(r.statusCode,r.headers);r.on('error',()=>res.destroy());r.pipe(res);});
    upstream.on('error',()=>{if(!res.destroyed&&!res.headersSent){res.writeHead(502);res.end();}else res.destroy();});
    req.on('aborted',()=>upstream.destroy());res.on('error',()=>upstream.destroy());res.on('close',()=>{if(!res.writableEnded)upstream.destroy();});req.pipe(upstream);return;
  }
  const path=resolve(root,'.'+new URL(req.url,'http://127.0.0.1').pathname);
  if(path!==root&&!path.startsWith(root+sep)){res.writeHead(403);res.end();return;}
  try {const file=extname(path)?path:resolve(root,'index.html');res.setHeader('Content-Type',types[extname(file)]??'application/octet-stream');res.end(await readFile(file));}
  catch{res.writeHead(404);res.end();}
}).listen(Number(portArg),'127.0.0.1');
