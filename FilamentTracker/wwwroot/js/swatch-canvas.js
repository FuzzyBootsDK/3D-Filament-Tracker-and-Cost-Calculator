(function(){
  // Simple cache
  window._swatchCache = window._swatchCache || {};

  function hexToRgb(hex8){
    if(!hex8) return {r:0,g:0,b:0,a:1};
    hex8 = hex8.replace('#','');
    if(hex8.length===6) hex8 = hex8 + 'FF';
    if(hex8.length!==8) return {r:0,g:0,b:0,a:1};
    const r = parseInt(hex8.substring(0,2),16);
    const g = parseInt(hex8.substring(2,4),16);
    const b = parseInt(hex8.substring(4,6),16);
    const a = parseInt(hex8.substring(6,8),16)/255;
    return {r,g,b,a};
  }

  function makeCanvas(w,h){
    const c = document.createElement('canvas');
    c.width = w; c.height = h; return c;
  }

  function noise(ctx,w,h, intensity=0.08){
    const img = ctx.createImageData(w,h);
    for(let i=0;i<img.data.length;i+=4){
      const v = (Math.random()*255)|0;
      img.data[i]=v; img.data[i+1]=v; img.data[i+2]=v; img.data[i+3]=(255*intensity)|0;
    }
    ctx.putImageData(img,0,0);
  }

  function generateMetallic(hex8,w,h){
    const key = `metal_${hex8}_${w}x${h}`;
    if(window._swatchCache[key]) return window._swatchCache[key];
    const col = hexToRgb(hex8);
    const c = makeCanvas(w,h); const ctx = c.getContext('2d');
    // base fill
    ctx.fillStyle = `rgba(${col.r},${col.g},${col.b},${col.a})`;
    ctx.fillRect(0,0,w,h);
    // add subtle linear noise
    const grad = ctx.createLinearGradient(0,0,w,h);
    grad.addColorStop(0, `rgba(255,255,255,0.12)`);
    grad.addColorStop(0.5, `rgba(255,255,255,0.02)`);
    grad.addColorStop(1, `rgba(0,0,0,0.08)`);
    ctx.fillStyle = grad; ctx.fillRect(0,0,w,h);
    // add specular streaks
    ctx.globalCompositeOperation = 'overlay';
    for(let i=0;i<3;i++){
      ctx.beginPath();
      ctx.moveTo(-w*0.2, Math.random()*h);
      ctx.bezierCurveTo(w*0.2, Math.random()*h, w*0.6, Math.random()*h, w*1.2, Math.random()*h);
      ctx.lineWidth = (2 + Math.random()*6);
      ctx.strokeStyle = `rgba(255,255,255,${0.07 + Math.random()*0.12})`;
      ctx.stroke();
    }
    // grain
    ctx.globalCompositeOperation = 'soft-light';
    noise(ctx,w,h,0.04);

    // restore
    ctx.globalCompositeOperation = 'source-over';
    const data = c.toDataURL('image/png');
    window._swatchCache[key]=data;
    return data;
  }

  function generateMarble(hex8,w,h){
    const key = `marble_${hex8}_${w}x${h}`;
    if(window._swatchCache[key]) return window._swatchCache[key];
    const col = hexToRgb(hex8);
    const c = makeCanvas(w,h); const ctx = c.getContext('2d');
    // base
    ctx.fillStyle = `rgba(${col.r},${col.g},${col.b},${col.a})`;
    ctx.fillRect(0,0,w,h);
    // light blur
    ctx.globalCompositeOperation = 'overlay';
    for(let i=0;i<6;i++){
      ctx.beginPath();
      const startY = Math.random()*h;
      ctx.moveTo(0, startY);
      const cp1x = w*0.25, cp1y = startY + (Math.random()-0.5)*h*0.4;
      const cp2x = w*0.75, cp2y = startY + (Math.random()-0.5)*h*0.4;
      ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, w, startY + (Math.random()-0.5)*20);
      ctx.lineWidth = 1 + Math.random()*6;
      ctx.strokeStyle = `rgba(255,255,255,${0.03 + Math.random()*0.06})`;
      ctx.stroke();
    }
    // darker veins
    ctx.globalCompositeOperation = 'multiply';
    for(let i=0;i<5;i++){
      ctx.beginPath();
      const startY = Math.random()*h;
      ctx.moveTo(0, startY);
      const cp1x = w*0.2, cp1y = startY + (Math.random()-0.5)*h*0.6;
      const cp2x = w*0.8, cp2y = startY + (Math.random()-0.5)*h*0.6;
      ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, w, startY + (Math.random()-0.5)*20);
      ctx.lineWidth = 1 + Math.random()*4;
      ctx.strokeStyle = `rgba(0,0,0,${0.04 + Math.random()*0.12})`;
      ctx.stroke();
    }
    // slight grain
    ctx.globalCompositeOperation = 'overlay';
    noise(ctx,w,h,0.03);
    ctx.globalCompositeOperation = 'source-over';
    const data = c.toDataURL('image/png');
    window._swatchCache[key]=data; return data;
  }

  // Public API
  window.swatch = window.swatch || {};
  window.swatch.generateTexture = function(variant, hex8, w, h){
    try{
      variant = (variant||'').toString(); hex8 = (hex8||'').toString(); w = parseInt(w)||64; h = parseInt(h)||64;
      if(variant==='metal') return generateMetallic(hex8,w,h);
      if(variant==='marble') return generateMarble(hex8,w,h);
      return '';
    }catch(e){ console.error('swatch.generateTexture',e); return ''; }
  };
})();
