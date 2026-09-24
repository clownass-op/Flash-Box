/* ---- drag-and-drop SWF loading (classic FlashBox slot boxes) ----
   Drop .swf files anywhere on the side panel: a row of slot boxes appears.
   Drop onto a box and each file loads straight into the player (never saved
   to disk). Weapon type for split sets is auto-detected by the host.

   EDIT ME: slots shown (order + labels) live in DROP_SLOTS below.
   Depends on index.html globals: setStatus(), post(). */

const DROP_SLOTS = ['Hair', 'Helm', 'Armor', 'Cape', 'Weapon', 'Pet'];

let __dropFiles = [];
let __dragDepth = 0;

function isFileDrag(e) {
  const t = e.dataTransfer && e.dataTransfer.types;
  return t && [...t].includes('Files');
}

document.addEventListener('dragenter', e => {
  if (!isFileDrag(e)) return;
  e.preventDefault();
  if (++__dragDepth === 1) showDropBoxes();
});
document.addEventListener('dragover', e => { e.preventDefault(); });
document.addEventListener('dragleave', e => {
  if (!isFileDrag(e)) return;
  if (--__dragDepth <= 0) {
    __dragDepth = 0;
    hideDropBoxes();
  }
});
document.addEventListener('drop', e => {
  __dragDepth = 0;
  hideDropBoxes();
  // NOTE: drops landing on a slot box are handled by that box's own handler
  // (stopPropagation) below. Anything else is a deliberate miss: just re-aim
  // at a box. Still cancel the default (WebView2 would navigate the page to
  // the dropped file).
  e.preventDefault();
});

function showDropBoxes() {
  let ov = document.getElementById('dropBoxes');
  if (!ov) {
    ov = document.createElement('div');
    ov.id = 'dropBoxes';
    ov.innerHTML = '<div class="row"></div><div class="hint">Drop a .swf file onto a slot</div>';
    document.body.appendChild(ov);
    const row = ov.querySelector('.row');
    DROP_SLOTS.forEach(t => {
      const cell = document.createElement('div');
      cell.className = 'cell';
      cell.dataset.slot = t;
      cell.innerHTML = '<div class="drop-label">' + t + '</div><div class="drop-box"></div>';
      const box = cell.querySelector('.drop-box');
      box.addEventListener('dragover', e => e.preventDefault());
      box.addEventListener('dragenter', e => { e.preventDefault(); cell.classList.add('over'); });
      box.addEventListener('dragleave', () => cell.classList.remove('over'));
      box.addEventListener('drop', e => {
        e.preventDefault();
        e.stopPropagation();
        const fs = e.dataTransfer && e.dataTransfer.files;
        __dropFiles = fs ? [...fs].filter(f => /\.swf$/i.test(f.name || '')) : [];
        if (!__dropFiles.length) {
          setStatus('No .swf files in drop.');
          hideDropBoxes();
          __dragDepth = 0;
          return;
        }
        dropAs(t);
      });
      row.appendChild(cell);
    });
  }
  ov.style.display = 'flex';
}

function hideDropBoxes() {
  const ov = document.getElementById('dropBoxes');
  if (ov) { ov.style.display = 'none'; ov.querySelectorAll('.cell.over').forEach(c => c.classList.remove('over')); }
}

async function dropAs(t) {
  hideDropBoxes();
  __dragDepth = 0;
  for (const f of __dropFiles) {
    setStatus('Loading dropped ' + f.name + ' as ' + t + '...');
    try {
      const b64 = await new Promise((res, rej) => {
        const r = new FileReader();
        r.onload = () => res(String(r.result).split(',')[1]);
        r.onerror = rej;
        r.readAsDataURL(f);
      });
      await post('dropItemBytes', { itemType: t, fileName: f.name, b64 });
    } catch (err) { setStatus('Drop failed: ' + f.name); return; }
  }
  setStatus('Drop loaded.');
  __dropFiles = [];
}
