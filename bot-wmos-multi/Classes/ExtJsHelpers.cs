
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WmosAutomatizacion.Classes
{
    public static class ExtJsHelpers
    {
        private const string LAYER_QUERY =
            "#ext-global-float-root, .x-layer, .x-floating, .x-floated, .x-float-wrap, .x-float-wrap *";

        // ===========================
        // Utilidad: convierte JSHandle -> ElementHandle de forma segura
        // ===========================
        private static IElementHandle? AsElementOrNull(IJSHandle? h) => h?.AsElement();

        // ===========================
        // Utilidad: esperar y devolver el primer elemento que cumpla en un frame
        // - Busca en document y en los "floating layers" de ExtJS
        // - Aplica normalización de texto
        // - Puede filtrar por tag (por defecto cualquiera)
        // - Respeta "visibleOnly"
        // ===========================
        private static async Task<IElementHandle?> FindElementByTextInFrameAsync(
            IFrame frame,
            string text,
            string? tagOrNull,
            bool exact,
            bool visibleOnly,
            int perAttemptTimeoutMs)
        {
            var js = @"
                (args) => {
                  const norm = (s) => (s || '')
                    .replace(/\u00A0/g, ' ')
                    .replace(/\s+/g, ' ')
                    .trim()
                    .toLowerCase();

                  const match = (el, target, exact) => {
                    const t = norm(el.textContent);
                    return exact ? t === norm(target) : t.includes(norm(target));
                  };

                  const isVisible = (el) => {
                    if (!(el instanceof Element)) return false;
                    const style = window.getComputedStyle(el);
                    const rect = el.getBoundingClientRect();
                    return style && style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
                  };

                  const roots = [document];
                  for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) {
                    roots.push(lay);
                  }

                  const selector = args.selector; // si es null busca cualquier tag
                  const exact = !!args.exact;
                  const requireVisible = !!args.visibleOnly;

                  const collect = (root) => {
                    let candidates;
                    if (selector) {
                      candidates = root.querySelectorAll(selector);
                    } else {
                      candidates = root.querySelectorAll('*');
                    }
                    for (const el of candidates) {
                      if (requireVisible && !isVisible(el)) continue;
                      if (match(el, args.text, exact)) return el;
                    }
                    return null;
                  };

                  for (const r of roots) {
                    const found = collect(r);
                    if (found) return found;
                  }
                  return null;
                }";

            var args = new
            {
                text,
                selector = tagOrNull,    // por ejemplo: "span", "div", etc. Si null -> cualquier tag
                exact,
                visibleOnly
            };

            try
            {
                var handle = await frame.WaitForFunctionAsync(js, args, new() { Timeout = perAttemptTimeoutMs });
                return AsElementOrNull(handle);
            }
            catch
            {
                return null;
            }
        }

        // ===========================
        // BÚSQUEDA EN TODOS LOS FRAMES CON REINTENTOS (timeout total)
        // ===========================
        private static async Task<IElementHandle?> FindElementByTextAcrossFramesAsync(
            IPage page,
            string text,
            string? tagOrNull = null,
            bool exact = true,
            bool visibleOnly = true,
            int totalTimeoutMs = 10000,
            int perAttemptTimeoutMs = 500)
        {
            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    var el = await FindElementByTextInFrameAsync(frame, text, tagOrNull, exact, visibleOnly, perAttemptTimeoutMs);
                    if (el != null)
                        return el;
                }

                // Pequeño "nudge" para forzar render/relayout
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

                if (page.ViewportSize != null)
                {
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);
                }

                await Task.Delay(200);
            }

            return null;
        }

        // ===========================
        // PUBLIC: Buscar TD por texto (igual que antes pero más tolerante)
        // ===========================
        public static async Task<IElementHandle?> FindCellInFramesAsync(
            IPage page,
            string text,
            int timeoutMs = 2500,
            bool allowContainsFallback = true)
        {
            // Reutilizamos el buscador genérico acotándolo a <td> y exact/contains
            // Primero exacto, luego contains
            var found = await FindElementByTextAcrossFramesAsync(page, text, "td", exact: true, visibleOnly: true, totalTimeoutMs: timeoutMs);
            if (found != null) return found;

            //found = await FindElementByTextAcrossFramesAsync(page, text, "td", exact: false, visibleOnly: true, totalTimeoutMs: timeoutMs);
            //return found;

            if (allowContainsFallback)
            {
                found = await FindElementByTextAcrossFramesAsync(
                    page, text, tagOrNull: "td", exact: false, visibleOnly: true, totalTimeoutMs: timeoutMs);
                if (found != null) return found;
            }

            return null;

        }


        public static async Task<IElementHandle?> RetryFindCellExactAsync(
            IPage page,
            string text,
            int attempts = 6,
            int delayMs = 700)
        {
            for (int i = 1; i <= attempts; i++)
            {
                var handle = await FindCellInFramesAsync(page, text, timeoutMs: 2500, allowContainsFallback: false);
                if (handle is not null) return handle;

                // Nudges (igual que tu otro retry)
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(delayMs);
                delayMs = Math.Min(delayMs + 400, 2000);
            }
            return null;
        }


        public static async Task<IElementHandle?> RetryFindCellAsync(IPage page, string text, int attempts = 6, int delayMs = 700)
        {
            for (int i = 1; i <= attempts; i++)
            {
                var handle = await FindCellInFramesAsync(page, text, timeoutMs: 2500);
                if (handle is not null) return handle;

                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(delayMs);
                delayMs = Math.Min(delayMs + 400, 2000);
            }
            return null;
        }

        // ===========================
        // PUBLIC: Click en <span> (o cualquier tag si no es span) y luego click en input del <td> siguiente
        // ===========================
        public static async Task ClickSpanAndNextInputAsync(
            IPage page,
            string? spanId = null,
            string? exactText = null,
            int totalTimeoutMs = 10000)
        {
            if (string.IsNullOrWhiteSpace(spanId) && string.IsNullOrWhiteSpace(exactText))
                throw new ArgumentException("Debes especificar spanId o exactText.");

            IElementHandle? span = null;

            // 1) Si hay ID, probar primero por ID (document + layers)
            if (!string.IsNullOrWhiteSpace(spanId))
            {
                var byIdJs = @"
                    (id) => {
                      const isVisible = (el) => {
                        const style = window.getComputedStyle(el);
                        const rect = el.getBoundingClientRect();
                        return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
                      };

                      const roots = [document];
                      for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) roots.push(lay);

                      for (const r of roots) {
                        const el = r.querySelector('#' + CSS.escape(id));
                        if (el && el.tagName === 'SPAN' && isVisible(el)) return el;
                      }
                      return null;
                    }";
                var start = DateTime.UtcNow;
                while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs && span == null)
                {
                    foreach (var frame in page.Frames)
                    {
                        try
                        {
                            var h = await frame.WaitForFunctionAsync(byIdJs, spanId, new() { Timeout = 500 });
                            span = AsElementOrNull(h);
                            if (span != null) break;
                        }
                        catch { /* seguir */ }
                    }
                    if (span == null) await Task.Delay(150);
                }
            }

            // 2) Si no hay ID o no apareció, buscar por texto:
            if (span == null && !string.IsNullOrWhiteSpace(exactText))
            {
                // Primero intentar como <span> exacto visible
                span = await FindElementByTextAcrossFramesAsync(page, exactText!, "span", exact: true, visibleOnly: true, totalTimeoutMs: totalTimeoutMs);
                // Si no, intentar contains y sin restringir a <span>
                if (span == null)
                    span = await FindElementByTextAcrossFramesAsync(page, exactText!, null, exact: false, visibleOnly: true, totalTimeoutMs: totalTimeoutMs);
            }

            if (span == null)
            {
                await page.ScreenshotAsync(new() { Path = "span_not_found.png", FullPage = true });
                // Dump opcional para debug:
                // Console.WriteLine(await page.ContentAsync());
                throw new Exception($"No se encontró un elemento visible con el texto {(exactText ?? spanId)} (no necesariamente <span>).");
            }

            // 3) Click seguro en el elemento encontrado
            await span.ScrollIntoViewIfNeededAsync();
            await span.ClickAsync();

            // 4) Obtener el siguiente <td> de la misma fila
            var nextTd = AsElementOrNull(await span.EvaluateHandleAsync(@"
            (el) => {
              const td = el.closest('td');
              if (!td) return null;
              let s = td.nextElementSibling;
              while (s) {
                if (s.tagName === 'TD') return s;
                s = s.nextElementSibling;
              }
              return null;
            }"));

            if (nextTd == null)
            {
                await page.ScreenshotAsync(new() { Path = "next_td_not_found.png", FullPage = true });
                throw new Exception("No se encontró el <td> siguiente al elemento clickeado.");
            }

            // 5) Buscar un input clickeable dentro del TD
            var input = AsElementOrNull(await nextTd.EvaluateHandleAsync(@"(td) => {
              const cand = td.querySelector('input, button, .x-btn, a');
              return cand || null;
            }"));

            if (input == null)
            {
                await page.ScreenshotAsync(new() { Path = "input_not_found.png", FullPage = true });
                throw new Exception("El <td> siguiente no contiene un input/clickeable.");
            }

            await input.ScrollIntoViewIfNeededAsync();
            await input.ClickAsync();
        }



        public static async Task ClickLinkByTextAsync(IPage page, string text)
        {
            foreach (var frame in page.Frames)
            {
                var locator = frame.Locator($"a:has-text('{text}')");

                if (await locator.First.IsVisibleAsync())
                {
                    await locator.First.ScrollIntoViewIfNeededAsync();
                    await locator.First.ClickAsync();
                    return;
                }
            }

            await page.ScreenshotAsync(new() { Path = $"link_{text}_not_found.png", FullPage = true });
            throw new Exception($"No se encontró el enlace con texto '{text}'.");
        }



  
        public static async Task SelectOptionByTrimmedTextAsync(
            IPage page,
            string selectIdOrCss,
            string visibleText,
            bool allowContainsFallback = false,
            int timeoutMs = 10000)
        {
            var css = BuildCssFromIdOrSelector(selectIdOrCss);
            var select = await FindSelectInAnyFrameAsync(page, css, timeoutMs);
            if (select is null)
            {
                await page.ScreenshotAsync(new() { Path = $"select_not_found_{Sanitize(selectIdOrCss)}.png", FullPage = true });
                throw new Exception($"No se encontró el <select> '{selectIdOrCss}' en ningún frame.");
            }

            await select.WaitForAsync(new() { State = WaitForSelectorState.Visible });

            // 1) Intento directo usando label EXACTO (después de Trim)
            var label = visibleText.Trim();
            var direct = await select.SelectOptionAsync(new[] { new SelectOptionValue { Label = label } });
            if (direct != null && direct.Any()) return;

            // 2) Buscar el value a partir del texto normalizado (NBSP + espacios)
            var value = await select.EvaluateAsync<string?>(@"(sel, target) => {
                const norm = s => (s || '')
                    .replace(/\u00A0/g, ' ')
                    .replace(/\s+/g, ' ')
                    .trim()
                    .toLowerCase();

                const wanted = norm(target);
                for (const opt of sel.querySelectorAll('option')) {
                    const lbl = norm(opt.textContent);
                    if (lbl === wanted) return opt.value;
                }
                return null;
            }", label);

            // 3) Fallback contains(normalizado) si se permite
            if (string.IsNullOrEmpty(value) && allowContainsFallback)
            {
                value = await select.EvaluateAsync<string?>(@"(sel, target) => {
                    const norm = s => (s || '')
                        .replace(/\u00A0/g, ' ')
                        .replace(/\s+/g, ' ')
                        .trim()
                        .toLowerCase();

                    const wanted = norm(target);
                    for (const opt of sel.querySelectorAll('option')) {
                        const lbl = norm(opt.textContent);
                        if (lbl.includes(wanted)) return opt.value;
                    }
                    return null;
                }", label);
            }

            if (string.IsNullOrEmpty(value))
            {
                await page.ScreenshotAsync(new() { Path = $"option_not_found_{Sanitize(label)}.png", FullPage = true });
                throw new Exception($"No se encontró una opción cuyo texto (normalizado) coincida con '{label}' en '{selectIdOrCss}'.");
            }

            var res = await select.SelectOptionAsync(new[] { new SelectOptionValue { Value = value } });
            if (res == null || !res.Any())
                    throw new Exception($"No se pudo seleccionar el value '{value}' derivado del texto '{label}'.");
        }

        // ===========================
        // 🔧 UTILIDADES PRIVADAS
        // ===========================
        private static string BuildCssFromIdOrSelector(string idOrSelector)
        {
            if (string.IsNullOrWhiteSpace(idOrSelector))
                throw new ArgumentException("idOrSelector no puede ser vacío.");

            // Si ya parece un selector CSS, úsalo tal cual.
            if (idOrSelector.StartsWith("#") || idOrSelector.StartsWith(".") ||
                idOrSelector.Contains(" ") || idOrSelector.Contains("[") ||
                idOrSelector.Contains(">") || idOrSelector.Contains(","))
            {
                return idOrSelector;
            }

            // Si parece un ID (incluye ":"), hay que escaparlo para CSS (#dataForm\\:id)
            var escaped = idOrSelector.Replace(":", "\\:");
            return $"#{escaped}";
        }

        private static async Task<ILocator?> FindSelectInAnyFrameAsync(IPage page, string css, int timeoutMs)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    var loc = frame.Locator(css).First;
                    if (await loc.CountAsync() > 0)
                    {
                        // Si hay más de uno, First; priorizamos visible
                        if (await loc.IsVisibleAsync())
                            return loc;

                        // Si no visible, igual retornamos para que se espere visibilidad arriba
                        return loc;
                    }
                }

                // pequeños nudges para forzar render
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                await Task.Delay(150);
            }
            return null;
        }

        private static string Sanitize(string s)
            => string.Concat((s ?? "").Split(System.IO.Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
               .Replace(" ", "_")
               .ToLowerInvariant();




        private static async Task<IElementHandle?> FindInputByValueInFrameAsync(
            IFrame frame,
            string value,
            string matchMode,   // "exact" o "prefix"
            bool visibleOnly,
            int perAttemptTimeoutMs)
        {
            var js = @"
              (args) => {
                const norm = (s) => (s || '')
                  .replace(/\u00A0/g, ' ')
                  .replace(/\s+/g, ' ')
                  .trim()
                  .toLowerCase();

                const isVisible = (el) => {
                  if (!(el instanceof Element)) return false;
                  const style = window.getComputedStyle(el);
                  const rect = el.getBoundingClientRect();
                  return style && style.display !== 'none' &&
                         style.visibility !== 'hidden' &&
                         rect.width > 0 && rect.height > 0;
                };

                const startsWithWordBoundary = (full, prefix) => {
                  // Acepta: igual, o empieza por prefix + espacio/nbsp/puntuación común
                  if (full === prefix) return true;
                  if (!full.startsWith(prefix)) return false;
                  const next = full.charAt(prefix.length);
                  return next === ' ' || next === '\u00A0' || /[.,;:()\-\[\]{}]/.test(next);
                };

                const match = (el, target, mode) => {
                  const v = norm(el.value || el.getAttribute('value') || '');
                  const t = norm(target);

                  if (mode === 'exact') {
                    return v === t;
                  }
                  if (mode === 'prefix') {
                    return startsWithWordBoundary(v, t);
                  }
                  // fallback defensivo
                  return v === t;
                };

                const roots = [document];
                for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) {
                  roots.push(lay);
                }

                const requireVisible = !!args.visibleOnly;
                const mode = args.matchMode;
                const target = args.value;

                for (const root of roots) {
                  const inputs = root.querySelectorAll('input[type=button], input[type=submit]');
                  for (const el of inputs) {
                    if (requireVisible && !isVisible(el)) continue;
                    if (match(el, target, mode)) return el;
                  }
                }
                return null;
              }";

            try
            {
                var handle = await frame.WaitForFunctionAsync(js,
                    new { value, matchMode, visibleOnly },
                    new() { Timeout = perAttemptTimeoutMs });

                return AsElementOrNull(handle);
            }
            catch
            {
                return null;
            }
        }




        private static async Task<IElementHandle?> FindInputByValueAcrossFramesAsync(
            IPage page,
            string value,
            string matchMode,  // "exact" o "prefix"
            bool visibleOnly = true,
            int totalTimeoutMs = 10000,
            int perAttemptTimeoutMs = 500)
        {
            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    var el = await FindInputByValueInFrameAsync(frame, value, matchMode, visibleOnly, perAttemptTimeoutMs);
                    if (el != null)
                        return el;
                }

                // Nudges (consistentes con tu helper)
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(200);
            }

            return null;
        }




        public static async Task ClickInputByValueAsync(
            IPage page,
            string visibleValue,
            bool allowSuffix = false,   // <- si true, usa match por prefijo (Submit, Submit ...).
            int timeoutMs = 8000)
        {
            // 1) Si NO permites sufijo -> exact
            if (!allowSuffix)
            {
                var exact = await FindInputByValueAcrossFramesAsync(
                    page, visibleValue, matchMode: "exact",
                    visibleOnly: true, totalTimeoutMs: timeoutMs);

                if (exact == null)
                {
                    await page.ScreenshotAsync(new() { Path = $"input_{Sanitize(visibleValue)}_not_found.png", FullPage = true });
                    throw new Exception($"No se encontró un <input> con value EXACTO '{visibleValue}'.");
                }

                await exact.ScrollIntoViewIfNeededAsync();
                try { await exact.WaitForElementStateAsync(ElementState.Enabled); } catch { }
                await exact.ClickAsync();
                return;
            }

            // 2) Si permites sufijo -> primero exact, luego prefijo (Submit, Submit Wave, Submit ...)
            var el = await FindInputByValueAcrossFramesAsync(
                page, visibleValue, matchMode: "exact",
                visibleOnly: true, totalTimeoutMs: timeoutMs);

            if (el == null)
            {
                el = await FindInputByValueAcrossFramesAsync(
                    page, visibleValue, matchMode: "prefix",
                    visibleOnly: true, totalTimeoutMs: timeoutMs);
            }

            if (el == null)
            {
                await page.ScreenshotAsync(new() { Path = $"input_{Sanitize(visibleValue)}_not_found.png", FullPage = true });
                throw new Exception($"No se encontró un <input> cuyo value comience por '{visibleValue}'.");
            }

            await el.ScrollIntoViewIfNeededAsync();
            try { await el.WaitForElementStateAsync(ElementState.Enabled); } catch { }
            await el.ClickAsync();
        }



        // ===========================
        // 🔎 Buscar <a> por ID dentro de un frame (incluye layers ExtJS)
        // ===========================
        private static async Task<IElementHandle?> FindAnchorByIdInFrameAsync(
            IFrame frame,
            string id,
            bool visibleOnly,
            int perAttemptTimeoutMs)
        {
            var js = @"
              (args) => {
                const isVisible = (el) => {
                  if (!(el instanceof Element)) return false;
                  const style = window.getComputedStyle(el);
                  const rect = el.getBoundingClientRect();
                  return style && style.display !== 'none' &&
                         style.visibility !== 'hidden' &&
                         rect.width > 0 && rect.height > 0;
                };

                const roots = [document];
                for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) {
                  roots.push(lay);
                }

                const requireVisible = !!args.visibleOnly;
                const targetId = args.id;

                for (const root of roots) {
                  const el = root.querySelector('#' + CSS.escape(targetId));
                  if (el && el.tagName === 'A') {
                    if (requireVisible && !isVisible(el)) continue;
                    return el;
                  }
                }
                return null;
              }";

            try
            {
                var handle = await frame.WaitForFunctionAsync(js,
                    new { id, visibleOnly },
                    new() { Timeout = perAttemptTimeoutMs });

                return AsElementOrNull(handle);
            }
            catch
            {
                return null;
            }
        }

        // ===========================
        // 🔎 Buscar <a> por ID en todos los frames con reintentos
        // ===========================
        private static async Task<IElementHandle?> FindAnchorByIdAcrossFramesAsync(
            IPage page,
            string id,
            bool visibleOnly = true,
            int totalTimeoutMs = 8000,
            int perAttemptTimeoutMs = 500)
        {
            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    var el = await FindAnchorByIdInFrameAsync(frame, id, visibleOnly, perAttemptTimeoutMs);
                    if (el != null)
                        return el;
                }

                // Nudges para forzar render/relayout (consistente con tu helper)
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(200);
            }

            return null;
        }

        // ===========================
        // 🔎 Fallback: buscar <a> cuyo href contenga el parámetro de wave (por si el ID cambia)
        // ===========================
        private static async Task<IElementHandle?> FindAnchorByHrefParamAcrossFramesAsync(
            IPage page,
            string paramContains, // e.g. "qf_SHIP_WAVE_PARM.SHIP_WAVE_NBR="
            bool visibleOnly = true,
            int totalTimeoutMs = 8000,
            int perAttemptTimeoutMs = 500)
        {
            var js = @"
            (args) => {
                const isVisible = (el) => {
                  if (!(el instanceof Element)) return false;
                  const style = window.getComputedStyle(el);
                  const rect = el.getBoundingClientRect();
                  return style && style.display !== 'none' &&
                         style.visibility !== 'hidden' &&
                         rect.width > 0 && rect.height > 0;
                };

                const roots = [document];
                for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) {
                  roots.push(lay);
                }

                const requireVisible = !!args.visibleOnly;
                const needle = String(args.needle || '').toLowerCase();

                for (const root of roots) {
                  const anchors = root.querySelectorAll('a[href]');
                  for (const a of anchors) {
                    const href = String(a.getAttribute('href') || '').toLowerCase();
                    if (!href.includes(needle)) continue;
                    if (requireVisible && !isVisible(a)) continue;
                    return a;
                  }
                }
                return null;
            }";

            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        var handle = await frame.WaitForFunctionAsync(js,
                            new { needle = paramContains, visibleOnly },
                            new() { Timeout = perAttemptTimeoutMs });

                        var el = AsElementOrNull(handle);
                        if (el != null) return el;
                    }
                    catch { /* seguir */ }
                }

                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                await Task.Delay(200);
            }

            return null;
        }

        // ===========================
        // 🔹 Público: Doble click a <a> por ID, devolver su texto (trim/normalizado)
        //     - Si no lo encuentra por ID, fallback por href con el parámetro de wave
        // ===========================
        public static async Task<string> DblClickAnchorAndGetTextAsync(
            IPage page,
            string anchorId,                   // e.g. "dataForm:AwvNbrRun"
            string? hrefParamFallback = "qf_SHIP_WAVE_PARM.SHIP_WAVE_NBR=",
            int timeoutMs = 8000)
        {
            // 1) Intentar por ID en todos los frames/layers
            var a = await FindAnchorByIdAcrossFramesAsync(page, anchorId, visibleOnly: true, totalTimeoutMs: timeoutMs);

            // 2) Fallback por patrón en href si no aparece por ID
            if (a == null && !string.IsNullOrEmpty(hrefParamFallback))
            {
                a = await FindAnchorByHrefParamAcrossFramesAsync(page, hrefParamFallback!, visibleOnly: true, totalTimeoutMs: timeoutMs);
            }

            if (a == null)
            {
                await page.ScreenshotAsync(new() { Path = $"anchor_{Sanitize(anchorId)}_not_found.png", FullPage = true });
                throw new Exception($"No se encontró el enlace <a> con id '{anchorId}' ni por patrón href '{hrefParamFallback}'.");
            }

            // 3) Obtener el texto visible del <a> (trim + normalización de espacios)
            var text = await a.EvaluateAsync<string>(@"(el) => {
                const norm = (s) => (s || '')
                  .replace(/\u00A0/g, ' ')
                  .replace(/\s+/g, ' ')
                  .trim();
                return norm(el.textContent);
            }");

            // 4) Doble click seguro
            await a.ScrollIntoViewIfNeededAsync();
            try { await a.WaitForElementStateAsync(ElementState.Enabled); } catch { /* algunos <a> no reportan estado */ }
            await a.DblClickAsync();

            return text;
        }



        private static async Task<IElementHandle?> FindRow0FifthTdSpanInFrameAsync(
            IFrame frame,
            bool visibleOnly,
            int perAttemptTimeoutMs)
            {
                    var js = @"
              (args) => {
                const norm = (s) => (s || '')
                  .replace(/\u00A0/g, ' ')
                  .replace(/\s+/g, ' ')
                  .trim();

                const isVisible = (el) => {
                  if (!(el instanceof Element)) return false;
                  const style = window.getComputedStyle(el);
                  const rect = el.getBoundingClientRect();
                  return style && style.display !== 'none' &&
                         style.visibility !== 'hidden' &&
                         rect.width > 0 && rect.height > 0;
                };

                const roots = [document];
                for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) {
                  roots.push(lay);
                }

                const requireVisible = !!args.visibleOnly;

                const findRow0 = (root) => {
                  // 1) Por PK oculto de la fila 0
                  const pk = root.querySelector('#' + CSS.escape('dataForm:listView:dataTable:0:PK_0'));
                  if (pk) {
                    const tr = pk.closest('tr');
                    if (tr) return tr;
                  }

                  // 2) Por default action button de la fila 0
                  const def = root.querySelector('#' + CSS.escape('dataForm:listView:dataTable:0:defaultactionbutton'));
                  if (def) {
                    const tr = def.closest('tr');
                    if (tr) return tr;
                  }

                  // 3) Fallback: por cualquier div de edición de la fila 0
                  const anyEdit = root.querySelector(""div[id^='dataForm:listView:dataTable_body_tr0_td']"");
                  if (anyEdit) {
                    const tr = anyEdit.closest('tr');
                    if (tr) return tr;
                  }

                  return null;
                };

                for (const root of roots) {
                  const row = findRow0(root);
                  if (!row) continue;

                  // 5° TD (1-based)
                  const td = row.querySelector('td:nth-of-type(5)');
                  if (!td) continue;

                  // Preferimos el <span> conocido si existe (id termina en :0:c0012)
                  const preferred = td.querySelector(""span[id$=':0:c0012']"");
                  if (preferred) {
                    if (requireVisible && !isVisible(preferred)) continue;
                    const txt = norm(preferred.textContent);
                    if (txt.length > 0) return preferred;
                  }

                  // Si no, tomamos el primer <span> visible con texto
                  const spans = td.querySelectorAll('span');
                  for (const sp of spans) {
                    if (requireVisible && !isVisible(sp)) continue;
                    const txt = norm(sp.textContent);
                    if (txt.length > 0) return sp;
                  }
                }

                return null;
              }";

            try
            {
                var handle = await frame.WaitForFunctionAsync(
                    js,
                    new { visibleOnly },
                    new() { Timeout = perAttemptTimeoutMs }
                );

                return AsElementOrNull(handle);
            }
            catch
            {
                return null;
            }
        }



        private static async Task<IElementHandle?> FindRow0FifthTdSpanAcrossFramesAsync(
    IPage page,
    bool visibleOnly = true,
    int totalTimeoutMs = 8000,
    int perAttemptTimeoutMs = 500)
        {
            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    var sp = await FindRow0FifthTdSpanInFrameAsync(frame, visibleOnly, perAttemptTimeoutMs);
                    if (sp != null)
                        return sp;
                }

                // Nudges (coherentes con tus patrones)
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(200);
            }

            return null;
        }



        public static async Task<string> GetRow0FifthTdStatusAndDblClickAsync(
            IPage page,
            int timeoutMs = 8000)
        {
            var sp = await FindRow0FifthTdSpanAcrossFramesAsync(
                page,
                visibleOnly: true,
                totalTimeoutMs: timeoutMs
            );

            if (sp == null)
            {
                await page.ScreenshotAsync(new() { Path = "row0_fifth_td_span_not_found.png", FullPage = true });
                throw new Exception("No se encontró el <span> dentro del 5° <td> de la fila 0.");
            }

            // Texto normalizado
            var statusText = await sp.EvaluateAsync<string>(@"
                (el) => (el.textContent || '')
                  .replace(/\u00A0/g, ' ')
                  .replace(/\s+/g, ' ')
                  .trim()
            ");

            // Doble click seguro
            await sp.ScrollIntoViewIfNeededAsync();
            try { await sp.WaitForElementStateAsync(ElementState.Enabled); } catch { /* algunos <span> no exponen estado */ }
            await sp.DblClickAsync();

            return statusText;
        }



 



        private static async Task<IElementHandle?> FindElementByExactIdInFrameAsync(
            IFrame frame,
            string id,
            bool visibleOnly,
            int perAttemptTimeoutMs)
        {
            var js = @"
              (args) => {
                const isVisible = (el) => {
                  if (!(el instanceof Element)) return false;
                  const style = window.getComputedStyle(el);
                  const rect = el.getBoundingClientRect();
                  return style && style.display !== 'none' &&
                         style.visibility !== 'hidden' &&
                         rect.width > 0 && rect.height > 0;
                };

                const roots = [document];
                for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) {
                  roots.push(lay);
                }

                const requireVisible = !!args.visibleOnly;
                const targetId = args.id;

                for (const root of roots) {
                  const el = root.querySelector('#' + CSS.escape(targetId));
                  if (el) {
                    if (requireVisible && !isVisible(el)) continue;
                    return el;
                  }
                }

                return null;
              }";

            try
            {
                var handle = await frame.WaitForFunctionAsync(js,
                    new { id, visibleOnly },
                    new() { Timeout = perAttemptTimeoutMs });

                return AsElementOrNull(handle);
            }
            catch
            {
                return null;
            }
        }



        private static async Task<IElementHandle?> FindElementByExactIdAcrossFramesAsync(
            IPage page,
            string id,
            bool visibleOnly = true,
            int totalTimeoutMs = 8000,
            int perAttemptTimeoutMs = 500)
        {
            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    var el = await FindElementByExactIdInFrameAsync(frame, id, visibleOnly, perAttemptTimeoutMs);
                    if (el != null)
                        return el;
                }

                // Nudges como los otros helpers
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(200);
            }

            return null;
        }



        public static async Task<string> GetCaptionValueAsync(
            IPage page,
            string spanId,
            int timeoutMs = 8000)
        {
            var el = await FindElementByExactIdAcrossFramesAsync(
                page,
                spanId,
                visibleOnly: true,
                totalTimeoutMs: timeoutMs
            );

            if (el == null)
            {
                await page.ScreenshotAsync(new() { Path = $"caption_{Sanitize(spanId)}_not_found.png", FullPage = true });
                throw new Exception($"No se encontró el elemento con id '{spanId}'.");
            }

            var text = await el.EvaluateAsync<string>(@"
                (node) => (node.textContent || '')
                  .replace(/\u00A0/g, ' ')
                  .replace(/\s+/g, ' ')
                  .trim()
            ");

            return text;
        }


        public static async Task<int> GetCaptionIntAsync(
            IPage page,
            string spanId,
            int defaultIfEmpty = 0,
            int timeoutMs = 8000)
        {
            var text = await GetCaptionValueAsync(page, spanId, timeoutMs);

            // Limpieza básica: quita separadores, espacios y cualquier no-dígito (por seguridad)
            // Si tu UI pudiera usar separadores como "1,234" o "1.234", esto lo vuelve "1234".
            var digitsOnly = new string(text.Where(char.IsDigit).ToArray());

            if (string.IsNullOrWhiteSpace(digitsOnly))
                return defaultIfEmpty;

            if (int.TryParse(digitsOnly, System.Globalization.NumberStyles.Integer,
                             System.Globalization.CultureInfo.InvariantCulture, out var value))
                return value;

            throw new Exception($"No se pudo convertir '{text}' (id '{spanId}') a entero.");
        }




        // ===========================
        // 🔹 Público (genérico): obtener el TEXTO de una celda por encabezado
        //    - tableIdPrefix: ej. "dataForm:tskLstTbl"
        //    - headerText: ej. "Released"
        //    - rowIndex: ej. 0 (primera fila)
        //    - Retorna: string? (null si no existe header o no hay datos)
        // ===========================
        public static async Task<string?> GetTableCellByHeaderTextAsync(
            IPage page,
            string tableIdPrefix,
            string headerText,
            int rowIndex = 0,
            int timeoutMs = 8000)
        {
            var text = await GetTableCellValueByHeaderAcrossFramesAsync(
                page,
                tableIdPrefix,
                headerText,
                rowIndex,
                visibleOnly: true,
                totalTimeoutMs: timeoutMs
            );

            if (string.IsNullOrWhiteSpace(text))
                return null;

            return text;
        }

        // ===========================
        // 🔹 Público (genérico): obtener la celda como ENTERO por encabezado
        //    - Limpia y parsea dígitos del texto (e.g., "2", "1,234", "1.234")
        //    - Retorna: int? (null si no existe header, no hay datos o no parsea)
        // ===========================
        public static async Task<int?> GetTableCellByHeaderIntAsync(
            IPage page,
            string tableIdPrefix,
            string headerText,
            int rowIndex = 0,
            int timeoutMs = 8000)
        {
            var text = await GetTableCellByHeaderTextAsync(
                page, tableIdPrefix, headerText, rowIndex, timeoutMs
            );

            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Extrae solo dígitos (y signo por si acaso), así "1,234" / "1.234" => "1234"
            var digits = new string(text.Where(ch => char.IsDigit(ch) || ch == '-').ToArray());
            if (string.IsNullOrEmpty(digits))
                return null;

            if (int.TryParse(digits, System.Globalization.NumberStyles.Integer,
                             System.Globalization.CultureInfo.InvariantCulture, out var value))
                return value;

            return null;
        }



        public static async Task DblClickLabelByTextAsync(
            IPage page,
            string text,
            int timeoutMs = 8000)
        {
            // 1) Intentar exacto en <label>
            var el = await FindElementByTextAcrossFramesAsync(
                page,
                text,
                tagOrNull: "label",
                exact: true,
                visibleOnly: true,
                totalTimeoutMs: timeoutMs
            );

            // 2) Fallback: contains en <label>
            if (el == null)
            {
                el = await FindElementByTextAcrossFramesAsync(
                    page,
                    text,
                    tagOrNull: "label",
                    exact: false,
                    visibleOnly: true,
                    totalTimeoutMs: timeoutMs
                );
            }

            // 3) Fallback más laxo: contains en cualquier tag (por si el título no es <label>)
            if (el == null)
            {
                el = await FindElementByTextAcrossFramesAsync(
                    page,
                    text,
                    tagOrNull: null, // cualquier tag
                    exact: false,
                    visibleOnly: true,
                    totalTimeoutMs: timeoutMs
                );
            }

            if (el == null)
            {
                await page.ScreenshotAsync(new() { Path = $"label_{Sanitize(text)}_not_found.png", FullPage = true });
                throw new Exception($"No se encontró un elemento visible con el texto '{text}' (label exact/contains o cualquier tag contains).");
            }

            await el.ScrollIntoViewIfNeededAsync();
            try { await el.WaitForElementStateAsync(ElementState.Enabled); } catch {  }
            await el.DblClickAsync();
        }





        private static async Task<IJSHandle?> GetTableCellValueByHeaderInFrameAsync(
    IFrame frame,
    string tableIdPrefix, // ej: "dataForm:tskLstTbl"
    string headerText,    // ej: "Released"
    int rowIndex,         // ej: 0 (primera fila)
    bool visibleOnly,
    int perAttemptTimeoutMs)
        {
            var js = @"
      (args) => {
        const norm = (s) => (s || '')
          .replace(/\u00A0/g, ' ')
          .replace(/\s+/g, ' ')
          .trim();

        const lower = (s) => norm(s).toLowerCase();

        const isVisible = (el) => {
          if (!(el instanceof Element)) return false;
          const st = window.getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
        };

        const roots = [document];
        for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) roots.push(lay);

        const reqVisible = !!args.visibleOnly;
        const tblId     = args.tableIdPrefix;
        const wantedHdr = lower(args.headerText);
        const rowIndex  = +args.rowIndex;

        for (const root of roots) {
          // TABLES
          const headTable = root.querySelector('#' + CSS.escape(tblId));
          const bodyTable = root.querySelector('#' + CSS.escape(tblId + '_body'));
          if (!headTable || !bodyTable) continue;

          // ==== HEADER: encontrar índice de columna por texto ====
          let colIndex = -1; // 0-based

          // 1) Caso típico: thead > tr > td/th
          const hdrRow = headTable.querySelector('thead tr');
          if (hdrRow) {
            const hdrCells = hdrRow.querySelectorAll('td, th');
            for (let i = 0; i < hdrCells.length; i++) {
              // Preferir el texto de un <span> interno si existe
              const sp = hdrCells[i].querySelector('span');
              const txt = sp ? lower(sp.textContent) : lower(hdrCells[i].textContent);
              if (txt === wantedHdr) { colIndex = i; break; }
            }
          }

          // 2) Fallback: buscar spans de header con patrón de id (e.g. *_colhdr_id2)
          if (colIndex === -1) {
            const hdrSpans = headTable.querySelectorAll(""span[id*='_colhdr_']"");
            for (const sp of hdrSpans) {
              if (lower(sp.textContent) === wantedHdr) {
                // Deduce colIndex por la posición del <span> dentro de las celdas
                const cell = sp.closest('td, th');
                if (cell) {
                  const cells = Array.from(cell.parentElement?.querySelectorAll('td, th') || []);
                  colIndex = cells.indexOf(cell);
                  if (colIndex !== -1) break;
                }
              }
            }
          }

          if (colIndex === -1) {
            // Header no existe en este root -> probar siguiente root
            continue;
          }

          // ==== BODY: verificar si hay datos (evitar nodataRow visible) ====
          const noDataRow = root.querySelector('#' + CSS.escape(tblId) + ':nodataRow');
          if (noDataRow && !noDataRow.classList.contains('trhide')) {
            // 'No data found' está visible
            return null;
          }

          // ==== BODY: obtener fila y celda ====
          const rows = bodyTable.querySelectorAll(""tbody > tr.advtbl_row, tbody > tr[class*='advtbl_row']"");
          if (!rows || rows.length === 0) {
            // no hay filas
            return null;
          }

          if (rowIndex < 0 || rowIndex >= rows.length) {
            // índice fuera de rango
            return null;
          }

          const row = rows[rowIndex];
          const tds = row.querySelectorAll('td, th');
          if (!tds || tds.length <= colIndex) {
            // la fila no tiene esa cantidad de celdas
            return null;
          }

          const td = tds[colIndex];
          if (reqVisible && !isVisible(td)) continue;

          // Obtener el primer <span> visible con texto dentro del TD
          const spans = td.querySelectorAll('span');
          for (const sp of spans) {
            if (reqVisible && !isVisible(sp)) continue;
            const raw = norm(sp.textContent);
            if (raw.length > 0) return raw;
          }

          // Si no hay span con texto, tomar texto del TD
          const tdText = norm(td.textContent);
          if (tdText) return tdText;

          // Si llegamos aquí, en este root no hay valor útil
        }

        return null;
      }";

            try
            {
                var handle = await frame.WaitForFunctionAsync(js,
                    new { tableIdPrefix, headerText, rowIndex, visibleOnly },
                    new() { Timeout = perAttemptTimeoutMs });

                return handle;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string?> GetTableCellValueByHeaderAcrossFramesAsync(
            IPage page,
            string tableIdPrefix,
            string headerText,
            int rowIndex,
            bool visibleOnly = true,
            int totalTimeoutMs = 8000,
            int perAttemptTimeoutMs = 500)
        {
            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    var h = await GetTableCellValueByHeaderInFrameAsync(
                        frame, tableIdPrefix, headerText, rowIndex, visibleOnly, perAttemptTimeoutMs);

                    if (h != null)
                    {
                        try
                        {
                            var val = await h.JsonValueAsync<string?>();
                            if (!string.IsNullOrEmpty(val))
                                return val;
                            // null/empty -> continúa
                        }
                        catch { /* continuar */ }
                    }
                }

                // Nudges (coherentes con tus helpers)
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(200);
            }

            return null;
        }


        private static async Task<IElementHandle?> FindByCssAcrossFramesAsync(
            IPage page,
            string cssSelector,
            bool visibleOnly = true,
            int totalTimeoutMs = 8000,
            int perAttemptTimeoutMs = 500)
        {
            var js = @"
      (args) => {
        const isVisible = (el) => {
          if (!(el instanceof Element)) return false;
          const st = window.getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' &&
                 st.visibility !== 'hidden' &&
                 rc.width > 0 && rc.height > 0;
        };

        const roots = [document];
        for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) {
          roots.push(lay);
        }

        const sel = String(args.selector || '');
        const requireVisible = !!args.visibleOnly;

        for (const root of roots) {
          const nodes = root.querySelectorAll(sel);
          for (const el of nodes) {
            if (requireVisible && !isVisible(el)) continue;
            return el;
          }
        }
        return null;
      }";

            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        var h = await frame.WaitForFunctionAsync(js,
                            new { selector = cssSelector, visibleOnly },
                            new() { Timeout = perAttemptTimeoutMs });

                        var el = AsElementOrNull(h);
                        if (el != null) return el;
                    }
                    catch { /* seguir */ }
                }

                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(200);
            }

            return null;
        }


        public static async Task ClickByCssAsync(
            IPage page,
            string cssSelector,
            int timeoutMs = 8000)
        {
            var el = await FindByCssAcrossFramesAsync(
                page,
                cssSelector,
                visibleOnly: true,
                totalTimeoutMs: timeoutMs
            );

            if (el == null)
            {
                await page.ScreenshotAsync(new() { Path = $"click_css_{Sanitize(cssSelector)}_not_found.png", FullPage = true });
                throw new Exception($"No se encontró elemento visible con selector '{cssSelector}'.");
            }

            await el.ScrollIntoViewIfNeededAsync();
            try { await el.WaitForElementStateAsync(ElementState.Enabled); } catch { }
            await el.ClickAsync();
        }



        public static async Task SelectOptionByCaptionAsync(
            IPage page,
            string captionText,         // ej: "Header Status"
            string optionVisibleText,   // ej: "Locked/Disabled"
            bool allowContainsFallback = false,
            int timeoutMs = 10000)
        {
            // 1) Encuentra el <select> dentro del mismo bloque de filtro cuyo caption contenga 'captionText'
            var selectHandle = await FindSelectByCaptionInFilterBlockAcrossFramesAsync(
                page,
                captionText,
                timeoutMs: timeoutMs
            );

            if (selectHandle == null)
            {
                await page.ScreenshotAsync(new() { Path = $"select_for_caption_{Sanitize(captionText)}_not_found.png", FullPage = true });
                throw new Exception($"No se encontró un <select> visible dentro del bloque de filtro con caption '{captionText}'.");
            }

            // 2) Seleccionar la opción por label/value (misma lógica que ya usabas)
            await selectHandle.ScrollIntoViewIfNeededAsync();
            await selectHandle.WaitForElementStateAsync(ElementState.Visible);

            var label = optionVisibleText.Trim();
            var direct = await selectHandle.SelectOptionAsync(new[] { new SelectOptionValue { Label = label } });
            if (direct != null && direct.Any()) return;

            // Buscar el value por texto normalizado
            var value = await selectHandle.EvaluateAsync<string?>(@"(sel, target) => {
        const norm = s => (s || '')
          .replace(/\u00A0/g, ' ')
          .replace(/\s+/g, ' ')
          .trim()
          .toLowerCase();

        const wanted = norm(target);
        for (const opt of sel.querySelectorAll('option')) {
            const lbl = norm(opt.textContent);
            if (lbl === wanted) return opt.value;
        }
        return null;
    }", label);

            // Fallback contains si se permite
            if (string.IsNullOrEmpty(value) && allowContainsFallback)
            {
                value = await selectHandle.EvaluateAsync<string?>(@"(sel, target) => {
            const norm = s => (s || '')
              .replace(/\u00A0/g, ' ')
              .replace(/\s+/g, ' ')
              .trim()
              .toLowerCase();

            const wanted = norm(target);
            for (const opt of sel.querySelectorAll('option')) {
                const lbl = norm(opt.textContent);
                if (lbl.includes(wanted)) return opt.value;
            }
            return null;
        }", label);
            }

            if (string.IsNullOrEmpty(value))
            {
                await page.ScreenshotAsync(new() { Path = $"option_for_caption_{Sanitize(label)}_not_found.png", FullPage = true });
                throw new Exception($"No se encontró una opción cuyo texto coincida con '{label}' para el caption '{captionText}'.");
            }

            var res = await selectHandle.SelectOptionAsync(new[] { new SelectOptionValue { Value = value } });
            if (res == null || !res.Any())
                throw new Exception($"No se pudo seleccionar el value '{value}' derivado del texto '{label}'.");
        }

        // === NUEVO: buscador preciso del <select> dentro del bloque de filtro con ese caption ===
        private static async Task<IElementHandle?> FindSelectByCaptionInFilterBlockAcrossFramesAsync(
            IPage page,
            string captionText,
            int timeoutMs = 10000,
            int perAttemptTimeoutMs = 500)
        {
            var js = @"
      (args) => {
        const norm = (s) => (s || '')
          .replace(/\u00A0/g, ' ')
          .replace(/\s+/g, ' ')
          .trim()
          .toLowerCase();

        const isVisible = (el) => {
          if (!(el instanceof Element)) return false;
          const st = window.getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
        };

        const roots = [document];
        for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) roots.push(lay);

        const wanted = norm(args.caption);

        for (const root of roots) {
          // Cada bloque de filtro suele agruparse así
          const blocks = root.querySelectorAll('.fltr_rightBdr.fltr_capShow');
          for (const block of blocks) {
            // Dentro del bloque, buscar el caption
            const cap = block.querySelector('.captionLeftNoWrap');
            if (!cap) continue;

            const capText = norm(cap.textContent);
            // tolerar ':' al final del caption y variaciones de espacio
            if (!capText.includes(wanted)) continue;

            // Buscar el SELECT dentro del MISMO bloque
            const selects = block.querySelectorAll('select');
            for (const sel of selects) {
              if (isVisible(sel)) return sel;
            }
          }
        }
        return null;
      }";

            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        var h = await frame.WaitForFunctionAsync(js,
                            new { caption = captionText },
                            new() { Timeout = perAttemptTimeoutMs });

                        var el = AsElementOrNull(h);
                        if (el != null) return el;
                    }
                    catch { /* continuar intentando */ }
                }

                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);
                await Task.Delay(200);
            }

            return null;
        }






        public static async Task ClickRowCheckboxByHeaderValueAsync(
    IPage page,
    string tableIdPrefix,       // ej: "dataForm:lview:dataTable"
    string headerText,          // ej: "Task Completion Reference Number"
    string targetValue,         // ej: waveNumber
    bool exact = true,          // true => igualdad exacta; false => contains
    int timeoutMs = 12000,
    int perAttemptTimeoutMs = 700)
        {
            var cb = await FindRowCheckboxByHeaderValueAcrossFramesAsync(
                page, tableIdPrefix, headerText, targetValue, exact, visibleOnly: true,
                totalTimeoutMs: timeoutMs, perAttemptTimeoutMs: perAttemptTimeoutMs);

            if (cb == null)
            {
                await page.ScreenshotAsync(new() { Path = $"checkbox_not_found_{Sanitize(headerText)}_{Sanitize(targetValue)}.png", FullPage = true });
                throw new Exception(
                    $"No se encontró checkbox para la fila donde '{headerText}' {(exact ? "==" : "contiene")} '{targetValue}'. " +
                    $"Se dejó una captura para revisar. Prueba exact=false o confirma que el valor realmente aparece en esa columna."
                );
            }

            await cb.ScrollIntoViewIfNeededAsync();
            try { await cb.WaitForElementStateAsync(ElementState.Enabled); } catch { }
            await cb.ClickAsync();
        }

        private static async Task<IElementHandle?> FindRowCheckboxByHeaderValueAcrossFramesAsync(
            IPage page,
            string tableIdPrefix,
            string headerText,
            string targetValue,
            bool exact,
            bool visibleOnly = true,
            int totalTimeoutMs = 12000,
            int perAttemptTimeoutMs = 700)
        {
            var js = @"
      (args) => {
        const norm = (s) => (s || '')
          .replace(/\u00A0/g, ' ')
          .replace(/\s+/g, ' ')
          .trim();
        const lower = (s) => norm(s).toLowerCase();

        const isVisible = (el) => {
          if (!(el instanceof Element)) return false;
          const st = window.getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
        };

        const roots = [document];
        for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) roots.push(lay);

        const tblId = String(args.tableIdPrefix || '');
        const wantedHdrExact = lower(args.headerText || '');
        const wantedHdrNeedle = wantedHdrExact.replace(/:+$/, ''); // tolera ':' al final
        const wantedValNorm = lower(args.targetValue || '');
        const requireVisible = !!args.visibleOnly;
        const exact = !!args.exact;

        const diag = {foundHeader:false, colIndex:-1, sample:[]};

        for (const root of roots) {
          const headTable = root.querySelector('#' + CSS.escape(tblId));
          const bodyTable = root.querySelector('#' + CSS.escape(tblId + '_body'));
          if (!headTable || !bodyTable) continue;

          // ====== 1) ColIndex por headerText (exacto) ======
          let colIndex = -1; // 0-based
          const hdrRow = headTable.querySelector('thead tr');
          if (hdrRow) {
            const hdrCells = hdrRow.querySelectorAll('td, th');
            for (let i = 0; i < hdrCells.length; i++) {
              const sp = hdrCells[i].querySelector('span');
              const txt = lower(sp ? sp.textContent : hdrCells[i].textContent);
              if (txt === wantedHdrExact || txt === wantedHdrNeedle) { colIndex = i; break; }
            }
          }

          // ====== 2) Fallback: contains en spans del header ======
          if (colIndex === -1) {
            const hdrSpans = headTable.querySelectorAll('span');
            for (const sp of hdrSpans) {
              const t = lower(sp.textContent);
              if (t === wantedHdrExact || t === wantedHdrNeedle || t.includes(wantedHdrNeedle)) {
                const cell = sp.closest('td, th');
                if (cell) {
                  const cells = Array.from(cell.parentElement?.querySelectorAll('td, th') || []);
                  colIndex = cells.indexOf(cell);
                  if (colIndex !== -1) break;
                }
              }
            }
          }

          if (colIndex === -1) {
            // probar siguiente root
            continue;
          }
          diag.foundHeader = true;
          diag.colIndex = colIndex;

          // ====== 3) Filas del body (amplio; evita nodataRow visibles) ======
          const tbody = bodyTable.querySelector('tbody');
          if (!tbody) continue;

          const allRows = Array.from(tbody.querySelectorAll('tr'));
          const rows = allRows.filter(r => {
            if (r.id && r.id.endsWith(':nodataRow')) return false;
            if (r.classList.contains('trhide')) return false;
            // si requiere visible, chequear
            return requireVisible ? isVisible(r) : true;
          });
          if (!rows.length) continue;

          // ====== 4) Diagnóstico: toma primeras 10 celdas de esa col ======
          for (let i = 0; i < Math.min(10, rows.length); i++) {
            const tds = rows[i].querySelectorAll('td, th');
            if (tds && tds.length > colIndex) {
              const td = tds[colIndex];
              let text = '';
              const spans = td.querySelectorAll('span');
              let took = false;
              for (const sp of spans) {
                if (requireVisible && !isVisible(sp)) continue;
                const txt = norm(sp.textContent);
                if (txt.length > 0) { text = txt; took = true; break; }
              }
              if (!took) text = norm(td.textContent);
              diag.sample.push(text);
            }
          }

          // ====== 5) Buscar la fila con match ======
          for (const row of rows) {
            const tds = row.querySelectorAll('td, th');
            if (!tds || tds.length <= colIndex) continue;

            const td = tds[colIndex];
            let cellText = '';
            const spans = td.querySelectorAll('span');
            let taken = false;
            for (const sp of spans) {
              if (requireVisible && !isVisible(sp)) continue;
              const txt = norm(sp.textContent);
              if (txt.length > 0) { cellText = txt; taken = true; break; }
            }
            if (!taken) cellText = norm(td.textContent);

            const ok = exact ? (lower(cellText) === wantedValNorm)
                             : (lower(cellText).includes(wantedValNorm));
            if (!ok) continue;

            // Match -> checkbox (suele estar en primera col)
            let cb = row.querySelector(""input[type='checkbox']"");
            if (cb && (!requireVisible || isVisible(cb))) {
              return cb;
            }
            const firstTd = tds[0];
            if (firstTd) {
              cb = firstTd.querySelector(""input[type='checkbox']"");
              if (cb && (!requireVisible || isVisible(cb))) {
                return cb;
              }
            }
          }

          // No match en este root; para diagnóstico, guardamos info en window para inspección manual
          try {
            window._extjsHelperDiag = window._extjsHelperDiag || {};
            window._extjsHelperDiag[tblId] = diag;
          } catch(e){}
        }

        return null;
      }";

            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        var h = await frame.WaitForFunctionAsync(js,
                            new
                            {
                                tableIdPrefix,
                                headerText,
                                targetValue,
                                exact,
                                visibleOnly
                            },
                            new() { Timeout = perAttemptTimeoutMs });

                        var el = AsElementOrNull(h);
                        if (el != null) return el;
                    }
                    catch { /* seguir */ }
                }

                // Nudges consistentes
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(220);
            }

            // Dump de diagnóstico si falla
            try
            {
                var diag = await page.EvaluateAsync<string?>(@"() => {
          const d = (window._extjsHelperDiag || {})['" + tableIdPrefix + @"'];
          if (!d) return null;
          return JSON.stringify(d);
        }");
                if (!string.IsNullOrEmpty(diag))
                    Console.WriteLine($"[DBG] Col match diag: {diag}");
            }
            catch { /* ignore */ }

            return null;
        }






        public static async Task<int> ClickAllRowCheckboxesByHeaderValueAsync(
    IPage page,
    string tableIdPrefix,       // ej: "dataForm:lview:dataTable"
    string headerText,          // ej: "Task Completion Reference Number"
    string targetValue,         // ej: waveNumber
    bool exact = true,          // true => igualdad exacta; false => contains
    int timeoutMs = 12000,
    int perAttemptTimeoutMs = 700)
        {
            var cbs = await FindAllRowCheckboxesByHeaderValueAcrossFramesAsync(
                page, tableIdPrefix, headerText, targetValue, exact, visibleOnly: true,
                totalTimeoutMs: timeoutMs, perAttemptTimeoutMs: perAttemptTimeoutMs);

            if (cbs == null || cbs.Count == 0)
            {
                await page.ScreenshotAsync(new() { Path = $"checkboxes_not_found_{Sanitize(headerText)}_{Sanitize(targetValue)}.png", FullPage = true });
                throw new Exception(
                    $"No se encontraron filas donde '{headerText}' {(exact ? "==" : "contiene")} '{targetValue}'. " +
                    $"Se dejó una captura. Si esperas múltiples páginas/scroll, avísame y añadimos auto-scroll."
                );
            }

            int clicked = 0;
            foreach (var cb in cbs)
            {
                try
                {
                    await cb.ScrollIntoViewIfNeededAsync();
                    try { await cb.WaitForElementStateAsync(ElementState.Enabled); } catch { }
                    await cb.ClickAsync();
                    clicked++;
                }
                catch
                {
                    // Si una fila falla, continúa con las demás
                }
            }

            return clicked;
        }



        private static async Task<List<IElementHandle>> FindAllRowCheckboxesByHeaderValueAcrossFramesAsync(
    IPage page,
    string tableIdPrefix,
    string headerText,
    string targetValue,
    bool exact,
    bool visibleOnly = true,
    int totalTimeoutMs = 12000,
    int perAttemptTimeoutMs = 700)
        {
            var js = @"
      (args) => {
        const norm = (s) => (s || '')
          .replace(/\u00A0/g, ' ')
          .replace(/\s+/g, ' ')
          .trim();
        const lower = (s) => norm(s).toLowerCase();

        const isVisible = (el) => {
          if (!(el instanceof Element)) return false;
          const st = window.getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
        };

        const roots = [document];
        for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) roots.push(lay);

        const tblId = String(args.tableIdPrefix || '');
        const wantedHdrExact = lower(args.headerText || '');
        const wantedHdrNeedle = wantedHdrExact.replace(/:+$/, ''); // tolera ':' al final
        const wantedValNorm = lower(args.targetValue || '');
        const requireVisible = !!args.visibleOnly;
        const exact = !!args.exact;

        const matches = [];

        for (const root of roots) {
          const headTable = root.querySelector('#' + CSS.escape(tblId));
          const bodyTable = root.querySelector('#' + CSS.escape(tblId + '_body'));
          if (!headTable || !bodyTable) continue;

          // ====== 1) ColIndex por headerText ======
          let colIndex = -1; // 0-based
          const hdrRow = headTable.querySelector('thead tr');
          if (hdrRow) {
            const hdrCells = hdrRow.querySelectorAll('td, th');
            for (let i = 0; i < hdrCells.length; i++) {
              const sp = hdrCells[i].querySelector('span');
              const txt = lower(sp ? sp.textContent : hdrCells[i].textContent);
              if (txt === wantedHdrExact || txt === wantedHdrNeedle) { colIndex = i; break; }
            }
          }
          if (colIndex === -1) {
            const hdrSpans = headTable.querySelectorAll('span');
            for (const sp of hdrSpans) {
              const t = lower(sp.textContent);
              if (t === wantedHdrExact || t === wantedHdrNeedle || t.includes(wantedHdrNeedle)) {
                const cell = sp.closest('td, th');
                if (cell) {
                  const cells = Array.from(cell.parentElement?.querySelectorAll('td, th') || []);
                  colIndex = cells.indexOf(cell);
                  if (colIndex !== -1) break;
                }
              }
            }
          }
          if (colIndex === -1) continue;

          // ====== 2) Filas del body ======
          const tbody = bodyTable.querySelector('tbody');
          if (!tbody) continue;

          const allRows = Array.from(tbody.querySelectorAll('tr'));
          const rows = allRows.filter(r => {
            if (r.id && r.id.endsWith(':nodataRow')) return false;
            if (r.classList.contains('trhide')) return false;
            return requireVisible ? isVisible(r) : true;
          });
          if (!rows.length) continue;

          // ====== 3) Para cada fila, validar y recolectar checkboxes ======
          for (const row of rows) {
            const tds = row.querySelectorAll('td, th');
            if (!tds || tds.length <= colIndex) continue;

            const td = tds[colIndex];
            let cellText = '';
            const spans = td.querySelectorAll('span');
            let taken = false;
            for (const sp of spans) {
              if (requireVisible && !isVisible(sp)) continue;
              const txt = norm(sp.textContent);
              if (txt.length > 0) { cellText = txt; taken = true; break; }
            }
            if (!taken) cellText = norm(td.textContent);

            const ok = exact ? (lower(cellText) === wantedValNorm)
                             : (lower(cellText).includes(wantedValNorm));
            if (!ok) continue;

            let cb = row.querySelector(""input[type='checkbox']"");
            if (!cb) {
              const firstTd = tds[0];
              if (firstTd) cb = firstTd.querySelector(""input[type='checkbox']"");
            }
            if (cb && (!requireVisible || isVisible(cb))) {
              matches.push(cb);
            }
          }
        }

        return matches; // array de nodos
      }";

            var start = DateTime.UtcNow;
            IJSHandle? arrHandle = null;

            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        arrHandle = await frame.WaitForFunctionAsync(js,
                            new
                            {
                                tableIdPrefix,
                                headerText,
                                targetValue,
                                exact,
                                visibleOnly
                            },
                            new() { Timeout = perAttemptTimeoutMs });

                        if (arrHandle is not null)
                        {
                            // Convertir array JS a lista de element handles
                            var props = await arrHandle.GetPropertiesAsync();
                            var list = new List<IElementHandle>();
                            foreach (var kv in props)
                            {
                                var el = kv.Value.AsElement();
                                if (el != null) list.Add(el);
                            }
                            if (list.Count > 0) return list;
                        }
                    }
                    catch { /* seguir */ }
                }

                // Nudges
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);

                await Task.Delay(220);
            }

            return new List<IElementHandle>();
        }



        public static async Task RightClickRowByHeaderValueAsync(
    IPage page,
    string tableIdPrefix,       // ej: "dataForm:listView:dataTable"
    string headerText,          // ej: "Wave Number"
    string targetValue,         // ej: waveNumber
    bool exact = true,
    int timeoutMs = 12000,
    int perAttemptTimeoutMs = 700)
        {
            var cell = await FindCellByHeaderValueAcrossFramesAsync(
                page, tableIdPrefix, headerText, targetValue, exact,
                visibleOnly: true, totalTimeoutMs: timeoutMs, perAttemptTimeoutMs: perAttemptTimeoutMs);

            if (cell != null)
            {
                await cell.ScrollIntoViewIfNeededAsync();
                try { await cell.WaitForElementStateAsync(ElementState.Visible); } catch { }
                await cell.ClickAsync(new ElementHandleClickOptions { Button = MouseButton.Right });
                return;
            }

            // Fallback: right-click en la fila completa
            var row = await FindRowByHeaderValueAcrossFramesAsync(
                page, tableIdPrefix, headerText, targetValue, exact,
                visibleOnly: true, totalTimeoutMs: timeoutMs, perAttemptTimeoutMs: perAttemptTimeoutMs);

            if (row == null)
            {
                await page.ScreenshotAsync(new() { Path = $"rightclick_not_found_{Sanitize(headerText)}_{Sanitize(targetValue)}.png", FullPage = true });
                throw new Exception($"No se encontró la fila para hacer click derecho donde '{headerText}' {(exact ? "==" : "contiene")} '{targetValue}'.");
            }

            await row.ScrollIntoViewIfNeededAsync();
            try { await row.WaitForElementStateAsync(ElementState.Visible); } catch { }
            await row.ClickAsync(new ElementHandleClickOptions { Button = MouseButton.Right });
        }


        // === Privados de soporte ===

        private static async Task<IElementHandle?> FindCellByHeaderValueAcrossFramesAsync(
            IPage page,
            string tableIdPrefix,
            string headerText,
            string targetValue,
            bool exact,
            bool visibleOnly = true,
            int totalTimeoutMs = 12000,
            int perAttemptTimeoutMs = 700)
        {
            var js = @"
      (args) => {
        const norm = (s) => (s || '').replace(/\u00A0/g,' ').replace(/\s+/g,' ').trim();
        const lower = (s) => norm(s).toLowerCase();
        const isVisible = (el) => {
          if (!(el instanceof Element)) return false;
          const st = window.getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
        };

        const roots = [document];
        for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) roots.push(lay);

        const tblId = String(args.tableIdPrefix || '');
        const wantedHdrExact = lower(args.headerText || '');
        const wantedHdrNeedle = wantedHdrExact.replace(/:+$/, '');
        const wantedValNorm = lower(args.targetValue || '');
        const requireVisible = !!args.visibleOnly;
        const exact = !!args.exact;

        for (const root of roots) {
          const headTable = root.querySelector('#' + CSS.escape(tblId));
          const bodyTable = root.querySelector('#' + CSS.escape(tblId + '_body'));
          if (!headTable || !bodyTable) continue;

          // Header -> colIndex
          let colIndex = -1;
          const hdrRow = headTable.querySelector('thead tr');
          if (hdrRow) {
            const hdrCells = hdrRow.querySelectorAll('td, th');
            for (let i = 0; i < hdrCells.length; i++) {
              const sp = hdrCells[i].querySelector('span');
              const txt = lower(sp ? sp.textContent : hdrCells[i].textContent);
              if (txt === wantedHdrExact || txt === wantedHdrNeedle) { colIndex = i; break; }
            }
          }
          if (colIndex === -1) {
            const hdrSpans = headTable.querySelectorAll('span');
            for (const sp of hdrSpans) {
              const t = lower(sp.textContent);
              if (t === wantedHdrExact || t === wantedHdrNeedle || t.includes(wantedHdrNeedle)) {
                const cell = sp.closest('td, th');
                if (cell) {
                  const cells = Array.from(cell.parentElement?.querySelectorAll('td, th') || []);
                  colIndex = cells.indexOf(cell);
                  if (colIndex !== -1) break;
                }
              }
            }
          }
          if (colIndex === -1) continue;

          const tbody = bodyTable.querySelector('tbody');
          if (!tbody) continue;

          const rows = Array.from(tbody.querySelectorAll('tr')).filter(r => {
            if (r.id && r.id.endsWith(':nodataRow')) return false;
            if (r.classList.contains('trhide')) return false;
            return requireVisible ? isVisible(r) : true;
          });

          for (const row of rows) {
            const tds = row.querySelectorAll('td, th');
            if (!tds || tds.length <= colIndex) continue;
            const td = tds[colIndex];

            let cellText = '';
            const spans = td.querySelectorAll('span');
            let taken = false;
            for (const sp of spans) {
              if (requireVisible && !isVisible(sp)) continue;
              const txt = norm(sp.textContent);
              if (txt.length > 0) { cellText = txt; taken = true; break; }
            }
            if (!taken) cellText = norm(td.textContent);

            const ok = exact ? (lower(cellText) === wantedValNorm)
                             : (lower(cellText).includes(wantedValNorm));
            if (ok) {
              return td; // devolvemos la celda
            }
          }
        }
        return null;
      }";

            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        var h = await frame.WaitForFunctionAsync(js,
                            new { tableIdPrefix, headerText, targetValue, exact, visibleOnly },
                            new() { Timeout = perAttemptTimeoutMs });

                        var el = AsElementOrNull(h);
                        if (el != null) return el;
                    }
                    catch { }
                }

                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);
                await Task.Delay(200);
            }
            return null;
        }


        private static async Task<IElementHandle?> FindRowByHeaderValueAcrossFramesAsync(
            IPage page,
            string tableIdPrefix,
            string headerText,
            string targetValue,
            bool exact,
            bool visibleOnly = true,
            int totalTimeoutMs = 12000,
            int perAttemptTimeoutMs = 700)
        {
            var js = @"
      (args) => {
        const norm = (s) => (s || '').replace(/\u00A0/g,' ').replace(/\s+/g,' ').trim();
        const lower = (s) => norm(s).toLowerCase();
        const isVisible = (el) => {
          if (!(el instanceof Element)) return false;
          const st = window.getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
        };

        const roots = [document];
        for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) roots.push(lay);

        const tblId = String(args.tableIdPrefix || '');
        const wantedHdrExact = lower(args.headerText || '');
        const wantedHdrNeedle = wantedHdrExact.replace(/:+$/, '');
        const wantedValNorm = lower(args.targetValue || '');
        const requireVisible = !!args.visibleOnly;
        const exact = !!args.exact;

        for (const root of roots) {
          const headTable = root.querySelector('#' + CSS.escape(tblId));
          const bodyTable = root.querySelector('#' + CSS.escape(tblId + '_body'));
          if (!headTable || !bodyTable) continue;

          // Header -> colIndex
          let colIndex = -1;
          const hdrRow = headTable.querySelector('thead tr');
          if (hdrRow) {
            const hdrCells = hdrRow.querySelectorAll('td, th');
            for (let i = 0; i < hdrCells.length; i++) {
              const sp = hdrCells[i].querySelector('span');
              const txt = lower(sp ? sp.textContent : hdrCells[i].textContent);
              if (txt === wantedHdrExact || txt === wantedHdrNeedle) { colIndex = i; break; }
            }
          }
          if (colIndex === -1) {
            const hdrSpans = headTable.querySelectorAll('span');
            for (const sp of hdrSpans) {
              const t = lower(sp.textContent);
              if (t === wantedHdrExact || t === wantedHdrNeedle || t.includes(wantedHdrNeedle)) {
                const cell = sp.closest('td, th');
                if (cell) {
                  const cells = Array.from(cell.parentElement?.querySelectorAll('td, th') || []);
                  colIndex = cells.indexOf(cell);
                  if (colIndex !== -1) break;
                }
              }
            }
          }
          if (colIndex === -1) continue;

          const tbody = bodyTable.querySelector('tbody');
          if (!tbody) continue;

          const rows = Array.from(tbody.querySelectorAll('tr')).filter(r => {
            if (r.id && r.id.endsWith(':nodataRow')) return false;
            if (r.classList.contains('trhide')) return false;
            return requireVisible ? isVisible(r) : true;
          });

          for (const row of rows) {
            const tds = row.querySelectorAll('td, th');
            if (!tds || tds.length <= colIndex) continue;
            const td = tds[colIndex];

            let cellText = '';
            const spans = td.querySelectorAll('span');
            let taken = false;
            for (const sp of spans) {
              if (requireVisible && !isVisible(sp)) continue;
              const txt = norm(sp.textContent);
              if (txt.length > 0) { cellText = txt; taken = true; break; }
            }
            if (!taken) cellText = norm(td.textContent);

            const ok = exact ? (lower(cellText) === wantedValNorm)
                             : (lower(cellText).includes(wantedValNorm));
            if (ok) return row;
          }
        }
        return null;
      }";

            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        var h = await frame.WaitForFunctionAsync(js,
                            new { tableIdPrefix, headerText, targetValue, exact, visibleOnly },
                            new() { Timeout = perAttemptTimeoutMs });

                        var el = AsElementOrNull(h);
                        if (el != null) return el;
                    }
                    catch { }
                }

                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                if (page.ViewportSize != null)
                    await page.Mouse.MoveAsync(page.ViewportSize.Width / 2, page.ViewportSize.Height / 2);
                await Task.Delay(200);
            }
            return null;
        }











        public static async Task SelectRowByHeaderValueEnsureToolbarAsync(
    IPage page,
    string tableIdPrefix,      // "dataForm:listView:dataTable"
    string headerText,         // "Wave Number"
    string targetValue,        // waveNumber
    bool exact = true,
    int timeoutMs = 12000)
        {
            // Encuentra la fila y su índice
            var (rowHandle, rowIndex) = await FindRowAndIndexByHeaderValueAcrossFramesAsync(
                page, tableIdPrefix, headerText, targetValue, exact,
                visibleOnly: true, totalTimeoutMs: timeoutMs, perAttemptTimeoutMs: 600);

            if (rowHandle == null || rowIndex < 0)
            {
                await page.ScreenshotAsync(new() { Path = "row_not_found_for_selection.png", FullPage = true });
                throw new Exception($"No se encontró la fila donde '{headerText}' {(exact ? "==" : "contiene")} '{targetValue}'.");
            }

            // (A) Click en la celda del header/valor (o fila)
            var cell = await FindCellByHeaderValueAcrossFramesAsync(
                page, tableIdPrefix, headerText, targetValue, exact,
                visibleOnly: true, totalTimeoutMs: 4000, perAttemptTimeoutMs: 400);

            if (cell != null)
            {
                await cell.ScrollIntoViewIfNeededAsync();
                try { await cell.WaitForElementStateAsync(ElementState.Visible); } catch { }
                await cell.ClickAsync(); // click simple sobre la celda
            }
            else
            {
                await rowHandle.ScrollIntoViewIfNeededAsync();
                await rowHandle.ClickAsync();
            }

            // (B) Intento de oficializar con FacesTable.selectRow(idx, true)
            var tableObjVar = await page.EvaluateAsync<string?>(@"() => {
      const keys = Object.keys(window);
      const cand = keys.find(k => k.includes('dataFormlistViewdataTable_tableObj'));
      return cand || null;
    }");

            if (!string.IsNullOrEmpty(tableObjVar))
            {
                try
                {
                    await page.EvaluateAsync(@"(args) => {
                const { objVar, idx } = args;
                const obj = window[objVar];
                if (obj && typeof obj.selectRow === 'function') {
                    obj.selectRow(idx, true);
                }
            }", new { objVar = tableObjVar!, idx = rowIndex });
                }
                catch { /* opcional: log */ }
            }

            // (C) Asegura hidden selectedRows con PK
            await page.EvaluateAsync(@"(args) => {
      const { tblId, idx } = args;
      const esc = s => s.replace(/:/g, '\\:');
      const pkSel = `#${esc(tblId)}_body tr:nth-of-type(${idx+1}) input[id$=':PK_${idx}']`;
      const pk = document.querySelector(pkSel);
      if (pk) {
        const ids = [
          `#${esc(tblId)}_selectedRows`,
          '#dataForm\\:listView\\:dataTable_selectedRows',
          '#dataForm\\:lview\\:dataTable_selectedRows'
        ];
        for (const id of ids) {
          const h = document.querySelector(id);
          if (h) { h.value = `#:#${pk.value}#:#`; }
        }
      }
    }", new { tblId = tableIdPrefix, idx = rowIndex });

            // (D) Dispara change/blur en checkboxes
            await page.EvaluateAsync(@"(args) => {
      const { tblId } = args;
      const body = document.querySelector('#' + CSS.escape(tblId) + '_body');
      if (!body) return;
      const checked = Array.from(body.querySelectorAll(""input[type='checkbox']:checked""));
      if (checked.length === 0) {
        const tr = body.querySelector('tr.-dg_tsr') || body.querySelector('tr');
        const cb = tr ? tr.querySelector(""input[type='checkbox']"") : null;
        if (cb) { cb.checked = true; }
      }
      const cbs = body.querySelectorAll(""input[type='checkbox']"");
      cbs.forEach(cb => {
        cb.dispatchEvent(new Event('change', { bubbles: true }));
        cb.dispatchEvent(new Event('blur', { bubbles: true }));
      });
    }", new { tblId = tableIdPrefix });

            await page.WaitForTimeoutAsync(300);
        }

        private static async Task<(IElementHandle? row, int index)> FindRowAndIndexByHeaderValueAcrossFramesAsync(
            IPage page,
            string tableIdPrefix,
            string headerText,
            string targetValue,
            bool exact,
            bool visibleOnly = true,
            int totalTimeoutMs = 8000,
            int perAttemptTimeoutMs = 500)
        {
            var row = await FindRowByHeaderValueAcrossFramesAsync(
                page, tableIdPrefix, headerText, targetValue, exact,
                visibleOnly, totalTimeoutMs, perAttemptTimeoutMs);

            if (row == null) return (null, -1);

            int idx = -1;
            try
            {
                idx = await row.EvaluateAsync<int>(@"(tr) => {
          const rows = Array.from(tr.parentElement?.querySelectorAll('tr') || []);
          return rows.indexOf(tr);
        }");
            }
            catch { /* ignore */ }

            return (row, idx);
        }




        public static async Task WaitAndClickInputByValueWhenEnabledAsync(
    IPage page,
    string visibleValue,
    int timeoutMs = 8000)
        {
            await page.WaitForFunctionAsync(@"(args) => {
      const val = args.val;
      const norm = s => (s||'').replace(/\u00A0/g,' ').replace(/\s+/g,' ').trim().toLowerCase();
      const wanted = norm(val);
      const isVisible = el => {
        if (!(el instanceof Element)) return false;
        const st = getComputedStyle(el);
        const rc = el.getBoundingClientRect();
        return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
      };
      const nodes = Array.from(document.querySelectorAll(""input[type='button'], input[type='submit']""));
      for (const el of nodes) {
        const v = norm(el.value || el.getAttribute('value') || '');
        if (v !== wanted) continue;
        if (!isVisible(el)) continue;
        if (el.disabled) continue;
        return true;
      }
      return false;
    }", new { val = visibleValue }, new() { Timeout = timeoutMs });

            await page.EvaluateAsync(@"(args) => {
      const val = args.val;
      const norm = s => (s||'').replace(/\u00A0/g,' ').replace(/\s+/g,' ').trim().toLowerCase();
      const wanted = norm(val);
      const isVisible = el => {
        if (!(el instanceof Element)) return false;
        const st = getComputedStyle(el);
        const rc = el.getBoundingClientRect();
        return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
      };
      const nodes = Array.from(document.querySelectorAll(""input[type='button'], input[type='submit']""));
      for (const el of nodes) {
        const v = norm(el.value || el.getAttribute('value') || '');
        if (v === wanted && isVisible(el) && !el.disabled) {
          el.click();
          return;
        }
      }
    }", new { val = visibleValue });
        }



        public static async Task InvokeCallActionMethodByInputValueAsync(
        IPage page,
        string visibleValue,
        int timeoutMs = 8000)
        {
            var actionId = await page.EvaluateAsync<string?>(@"(args) => {
      const val = args.val;
      const norm = s => (s||'').replace(/\u00A0/g,' ').replace(/\s+/g,' ').trim().toLowerCase();
      const wanted = norm(val);
      const nodes = document.querySelectorAll(""input[type='button'], input[type='submit']"");
      for (const el of nodes) {
        const v = norm(el.value || el.getAttribute('value') || '');
        if (v !== wanted) continue;
        const oc = el.getAttribute('onclick') || '';
        const m = oc.match(/callActionMethod\\('([^']+)'\\)/);
        if (m) return m[1];
      }
      return null;
    }", new { val = visibleValue });

            if (string.IsNullOrEmpty(actionId))
                throw new Exception($"No fue posible extraer el id de callActionMethod(...) para '{visibleValue}'.");

            await page.EvaluateAsync("(args) => { const id = args.id; if (window.callActionMethod) callActionMethod(id); }",
                new { id = actionId });
        }
















        /// <summary>
        /// Selecciona TODAS las filas donde la columna (headerText) coincide con targetValue.
        /// - Hace click VISUAL en cada checkbox coincidente.
        /// - Oficializa la selección para WMOS (tableObj.selectRow(idx, true) + *_selectedRows).
        /// - Dispara eventos change/blur y hace nudge de UI.
        /// Retorna la cantidad de checkboxes clickeados.
        /// </summary>
        public static async Task<int> SelectAllRowsOfficiallyAndVisuallyAsync(
            IPage page,
            string tableIdPrefix,       // ej: "dataForm:lview:dataTable"
            string headerText,          // ej: "Task Generation Reference Number"
            string targetValue,         // ej: waveNumber
            bool exact = true,
            int timeoutMs = 12000)
        {
            // 1) Encuentra TODOS los checkboxes de filas que matchean
            var checkboxes = await FindAllRowCheckboxesByHeaderValueAcrossFramesAsync(
                page, tableIdPrefix, headerText, targetValue, exact,
                visibleOnly: true, totalTimeoutMs: timeoutMs, perAttemptTimeoutMs: 700);

            if (checkboxes == null || checkboxes.Count == 0)
            {
                await page.ScreenshotAsync(new() { Path = $"no_rows_for_{Sanitize(headerText)}_{Sanitize(targetValue)}.png", FullPage = true });
                throw new Exception($"No se encontraron filas donde '{headerText}' {(exact ? "==" : "contiene")} '{targetValue}'.");
            }

            // 2) Click VISUAL + eventos en cada checkbox
            int clicked = 0;
            var rowIndices = new List<int>();

            foreach (var cb in checkboxes)
            {
                try
                {
                    await cb.ScrollIntoViewIfNeededAsync();
                    try { await cb.WaitForElementStateAsync(ElementState.Enabled); } catch { }
                    await cb.ClickAsync();

                    // Dispara eventos para WMOS
                    try
                    {
                        await cb.EvaluateAsync(@"(el) => {
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                    el.dispatchEvent(new Event('blur', { bubbles: true }));
                }");
                    }
                    catch { /* tolerante */ }

                    clicked++;

                    // Captura el índice de la fila para oficializar luego
                    try
                    {
                        var trHandle = await cb.EvaluateHandleAsync("(el) => el.closest('tr')");
                        var tr = trHandle.AsElement();
                        if (tr != null)
                        {
                            var idx = await tr.EvaluateAsync<int>(@"(row) => {
                        const rows = Array.from(row.parentElement?.querySelectorAll('tr') || []);
                        return rows.indexOf(row);
                    }");
                            if (idx >= 0) rowIndices.Add(idx);
                        }
                    }
                    catch { /* seguir */ }
                }
                catch
                {
                    // Si un checkbox falla, continuamos con los demás
                }
            }

            if (clicked == 0)
            {
                await page.ScreenshotAsync(new() { Path = "checkbox_clicks_failed.png", FullPage = true });
                throw new Exception("No fue posible clickear visualmente los checkboxes coincidentes.");
            }

            // 3) selectRow oficial (append=true) sobre cada índice si existe tableObj
            var tableObjVar = await page.EvaluateAsync<string?>(@"(args) => {
        const esc = s => s.replace(/:/g,'');
        const needle = esc(args.tbl) + '_tableObj';
        const keys = Object.keys(window);
        return keys.find(k => k.includes(needle)) || null;
    }", new { tbl = tableIdPrefix });

            if (!string.IsNullOrEmpty(tableObjVar) && rowIndices.Count > 0)
            {
                try
                {
                    await page.EvaluateAsync(@"(args) => {
                const { objVar, idxs } = args;
                const obj = window[objVar];
                if (!obj || typeof obj.selectRow !== 'function') return;
                for (const i of idxs) {
                    try { obj.selectRow(i, true); } catch(e){}
                }
            }", new { objVar = tableObjVar!, idxs = rowIndices });
                }
                catch { /* tolerante */ }
            }

            // 4) Asegurar hidden selectedRows (multi) -> "#:#pk1#:#pk2#:#..."
            await page.EvaluateAsync(@"(args) => {
        const { tblId } = args;
        const esc = s => s.replace(/:/g,'\\:');
        const bodySel = '#' + esc(tblId) + '_body';
        const body = document.querySelector(bodySel);
        if (!body) return;

        const rows = Array.from(body.querySelectorAll('tr'));
        const selectedPks = [];

        rows.forEach((tr, i) => {
            const cb = tr.querySelector(""input[type='checkbox']"");
            if (!cb || !cb.checked) return;

            let pk = tr.querySelector(`input[id$=':PK_${i}']`);
            if (!pk) {
                const hid = tr.querySelectorAll('input[type=hidden]');
                pk = hid && hid.length ? hid[0] : null;
            }
            if (pk && pk.value) selectedPks.push(pk.value);

            try { tr.classList.add('-dg_tsr'); } catch(e){}
        });

        const candidates = [
            `#${esc(tblId)}_selectedRows`,
            '#dataForm\\:lview\\:dataTable_selectedRows',
            '#dataForm\\:listView\\:dataTable_selectedRows'
        ];
        for (const sel of candidates) {
            const h = document.querySelector(sel);
            if (h && selectedPks.length) {
                h.value = selectedPks.map(v => `#:#${v}#:#`).join('');
            }
        }
    }", new { tblId = tableIdPrefix });

            // 5) Disparar change/blur global en la tabla para que la toolbar reaccione
            await page.EvaluateAsync(@"(args) => {
        const { tblId } = args;
        const body = document.querySelector('#' + CSS.escape(tblId) + '_body');
        if (!body) return;
        const cbs = body.querySelectorAll(""input[type='checkbox']"");
        cbs.forEach(cb => {
            cb.dispatchEvent(new Event('change', { bubbles: true }));
            cb.dispatchEvent(new Event('blur', { bubbles: true }));
        });
    }", new { tblId = tableIdPrefix });

            // 6) Nudge UI
            await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
            await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
            await page.WaitForTimeoutAsync(250);

            return clicked;
        }

        /// <summary>
        /// Selecciona todas las filas (visual + oficial) y luego espera/hace click en el botón indicado (por su value).
        /// Retorna cuántas filas fueron seleccionadas.
        /// </summary>
        public static async Task<int> SelectAllRowsAndClickActionAsync(
            IPage page,
            string tableIdPrefix,
            string headerText,
            string targetValue,
            string actionButtonValue = "Release Task",
            bool exact = true,
            int timeoutMs = 12000)
        {
            var count = await SelectAllRowsOfficiallyAndVisuallyAsync(
                page, tableIdPrefix, headerText, targetValue, exact, timeoutMs);

            await WaitAndClickInputByValueWhenEnabledAsync(page, actionButtonValue, timeoutMs);
            return count;
        }

        /// <summary>
        /// (Útil para 1 fila) Selección visual + oficial de UNA fila.
        /// </summary>
        public static async Task SelectRowOfficiallyAndVisuallyAsync(
            IPage page,
            string tableIdPrefix, // "dataForm:lview:dataTable"
            string headerText,    // "Task Generation Reference Number"
            string targetValue,   // waveNumber
            bool exact = true,
            int timeoutMs = 12000)
        {
            var (rowHandle, rowIndex) = await FindRowAndIndexByHeaderValueAcrossFramesAsync(
                page, tableIdPrefix, headerText, targetValue, exact,
                visibleOnly: true, totalTimeoutMs: timeoutMs, perAttemptTimeoutMs: 600);

            if (rowHandle == null || rowIndex < 0)
            {
                await page.ScreenshotAsync(new() { Path = "row_not_found_for_selection.png", FullPage = true });
                throw new Exception($"No se encontró la fila donde '{headerText}' {(exact ? "==" : "contiene")} '{targetValue}'.");
            }

            // Click en la fila para focus/scroll
            await rowHandle.ScrollIntoViewIfNeededAsync();
            try { await rowHandle.WaitForElementStateAsync(ElementState.Visible); } catch { }
            await rowHandle.ClickAsync();

            // Click VISUAL en checkbox (si existe)
            var cb = await rowHandle.QuerySelectorAsync("input[type='checkbox']");
            if (cb != null)
            {
                await cb.ScrollIntoViewIfNeededAsync();
                try { await cb.WaitForElementStateAsync(ElementState.Enabled); } catch { }
                await cb.ClickAsync();

                // Eventos para binding WMOS
                try
                {
                    await cb.EvaluateAsync(@"(el) => {
                el.dispatchEvent(new Event('change', { bubbles: true }));
                el.dispatchEvent(new Event('blur', { bubbles: true }));
            }");
                }
                catch { /* tolerante */ }
            }

            // Oficializa: tableObj.selectRow(idx,true)
            var tableObjVar = await page.EvaluateAsync<string?>(@"(args) => {
        const esc = s => s.replace(/:/g,'');
        const needle = esc(args.tbl) + '_tableObj';
        const keys = Object.keys(window);
        return keys.find(k => k.includes(needle)) || null;
    }", new { tbl = tableIdPrefix });

            if (!string.IsNullOrEmpty(tableObjVar))
            {
                try
                {
                    await page.EvaluateAsync(@"(args) => {
                const obj = window[args.objVar];
                if (obj && typeof obj.selectRow === 'function') {
                    obj.selectRow(args.idx, true);
                }
            }", new { objVar = tableObjVar!, idx = rowIndex });
                }
                catch { /* tolerante */ }
            }

            // selectedRows con el PK de la fila
            await page.EvaluateAsync(@"(args) => {
        const { tblId, idx } = args;
        const esc = s => s.replace(/:/g,'\\:');
        const body = document.querySelector('#' + esc(tblId) + '_body');
        if (!body) return;

        const tr = body.querySelector(`tr:nth-of-type(${idx+1})`);
        if (!tr) return;

        let pk = tr.querySelector(`input[id$=':PK_${idx}']`);
        if (!pk) {
            const hid = tr.querySelectorAll('input[type=hidden]');
            pk = hid && hid.length ? hid[0] : null;
        }
        if (!pk || !pk.value) return;

        const candidates = [
            `#${esc(tblId)}_selectedRows`,
            '#dataForm\\:lview\\:dataTable_selectedRows',
            '#dataForm\\:listView\\:dataTable_selectedRows'
        ];
        for (const sel of candidates) {
            const h = document.querySelector(sel);
            if (h) h.value = `#:#${pk.value}#:#`;
        }
        try { tr.classList.add('-dg_tsr'); } catch(e){}
    }", new { tblId = tableIdPrefix, idx = rowIndex });

            // change/blur global + nudge
            await page.EvaluateAsync(@"(args) => {
        const { tblId } = args;
        const body = document.querySelector('#' + CSS.escape(tblId) + '_body');
        if (!body) return;
        const cbs = body.querySelectorAll(""input[type='checkbox']"");
        cbs.forEach(cb => {
            cb.dispatchEvent(new Event('change', { bubbles: true }));
            cb.dispatchEvent(new Event('blur', { bubbles: true }));
        });
    }", new { tblId = tableIdPrefix });

            await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
            await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
            await page.WaitForTimeoutAsync(200);
        }














        /// <summary>
        /// Intenta un click inmediato (sin esperar) sobre un input[type=button|submit] por su value visible.
        /// Retorna true si encontró y clickeó; false si no encontró o estaba deshabilitado/oculto.
        /// </summary>
        public static async Task<bool> TryClickInputByValueOnceAsync(IPage page, string visibleValue)
        {
            try
            {
                var clicked = await page.EvaluateAsync<bool>(@"(args) => {
          const val = String(args.val || '');
          const norm = s => (s||'').replace(/\u00A0/g,' ').replace(/\s+/g,' ').trim().toLowerCase();
          const wanted = norm(val);

          const isVisible = el => {
            if (!(el instanceof Element)) return false;
            const st = getComputedStyle(el);
            const rc = el.getBoundingClientRect();
            return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
          };

          const nodes = Array.from(document.querySelectorAll(""input[type='button'], input[type='submit']""));
          for (const el of nodes) {
            const v = norm(el.value || el.getAttribute('value') || '');
            if (v !== wanted) continue;
            if (!isVisible(el)) continue;
            if (el.disabled) continue;
            el.click();
            return true;
          }
          return false;
        }", new { val = visibleValue });

                return clicked;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Hace clicks repetidos en el botón (por su value visible) hasta que aparezca un Dialog
        /// (alert/confirm/prompt de JS) y lo acepta automáticamente.
        /// - Con backoff progresivo y "nudges" de UI (resize + RAF) para forzar relayout.
        /// - Devuelve el texto del diálogo aceptado (por si quieres loguearlo).
        /// Lanza excepción si no aparece el diálogo dentro del tiempo total.
        /// </summary>
        public static async Task<string> ClickInputByValueUntilDialogAndAcceptAsync(
            IPage page,
            string visibleValue,
            int totalTimeoutMs = 15000,
            int minIntervalMs = 300,
            int maxIntervalMs = 1200,
            bool ensureEnabledBeforeFirstClick = false)
        {
            // 1) Opcional: esperar una vez a que esté habilitado antes del primer intento
            if (ensureEnabledBeforeFirstClick)
            {
                try { await WaitAndClickInputByValueWhenEnabledAsync(page, visibleValue, Math.Min(totalTimeoutMs, 4000)); }
                catch { /* si falla, igual seguimos con ciclo manual */ }
            }

            // 2) Preparar TCS y handler para capturar y aceptar el diálogo ni bien aparezca
            var tcs = new TaskCompletionSource<(string message, bool accepted)>(TaskCreationOptions.RunContinuationsAsynchronously);

            async void Handler(object? sender, IDialog dialog)
            {
                try
                {
                    var msg = dialog.Message ?? string.Empty;
                    await dialog.AcceptAsync(); // aceptar inmediatamente
                    tcs.TrySetResult((msg, true));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    // quitar handler para evitar fugas
                    page.Dialog -= Handler;
                }
            }

            page.Dialog += Handler;

            // 3) Bucle de reintentos con backoff y nudges
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int interval = Math.Max(100, Math.Min(minIntervalMs, maxIntervalMs)); // arranque prudente

            try
            {
                while (sw.ElapsedMilliseconds < totalTimeoutMs)
                {
                    // Intento de click "rápido"
                    _ = await TryClickInputByValueOnceAsync(page, visibleValue);

                    // Esperar o salir si apareció el diálogo
                    var remaining = (int)Math.Max(0, totalTimeoutMs - sw.ElapsedMilliseconds);
                    var delay = Math.Min(interval, remaining);

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(delay));
                    if (completed == tcs.Task)
                    {
                        // Diálogo capturado y aceptado
                        var result = await tcs.Task;
                        if (result.accepted) return result.message;
                        // por seguridad: aunque no debiera entrar acá si AcceptAsync falla
                        throw new Exception("El diálogo apareció pero no pudo ser aceptado.");
                    }

                    // Nudge de UI y aumentar intervalo (backoff suave)
                    try
                    {
                        await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                        await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                    }
                    catch { /* tolerante */ }

                    interval = Math.Min(interval + 150, maxIntervalMs);
                }

                // Si agotó el tiempo sin diálogo:
                await page.ScreenshotAsync(new() { Path = $"dialog_not_shown_clicking_{Sanitize(visibleValue)}.png", FullPage = true });
                throw new TimeoutException($"No apareció el diálogo al clicar repetidamente '{visibleValue}' dentro de {totalTimeoutMs} ms.");
            }
            finally
            {
                // Seguridad: remover handler en cualquier caso
                page.Dialog -= Handler;
            }
        }









        public enum ClickMode
        {
            Single, // un click
            Double, // doble click
            Both    // intenta single y luego double en la misma iteración
        }



        private static async Task<IElementHandle?> FindActionInputByValueAcrossFramesAsync(
    IPage page,
    string visibleValue,
    bool requireEnabled = true,
    int perAttemptTimeoutMs = 600)
        {
            var js = @"
      (args) => {
        const val = String(args.val || '');
        const norm = s => (s||'').replace(/\u00A0/g,' ').replace(/\s+/g,' ').trim().toLowerCase();
        const wanted = norm(val);
        const isVisible = el => {
          if (!(el instanceof Element)) return false;
          const st = getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
        };

        const nodes = Array.from(document.querySelectorAll(""input[type='button'], input[type='submit']""));
        for (const el of nodes) {
          const v = norm(el.value || el.getAttribute('value') || '');
          if (v !== wanted) continue;
          if (!isVisible(el)) continue;
          if (args.reqEnabled && el.disabled) continue;
          return el;
        }
        return null;
      }";

            foreach (var frame in page.Frames)
            {
                try
                {
                    var h = await frame.WaitForFunctionAsync(
                        js,
                        new { val = visibleValue, reqEnabled = requireEnabled },
                        new() { Timeout = perAttemptTimeoutMs });

                    var el = h?.AsElement();
                    if (el != null) return el;
                }
                catch { /* probar siguiente frame */ }
            }
            return null;
        }





        public static async Task<string> ClickInputByValueUntilDialogAndAcceptAsync(
    IPage page,
    string visibleValue,
    int totalTimeoutMs = 15000,
    int minIntervalMs = 300,
    int maxIntervalMs = 1200,
    bool ensureEnabledBeforeFirstClick = false,
    ClickMode clickMode = ClickMode.Double) // 👈 por defecto usamos doble click
        {
            // 1) (Opcional) Esperar una vez a que esté habilitado antes del primer intento
            if (ensureEnabledBeforeFirstClick)
            {
                try { await WaitAndClickInputByValueWhenEnabledAsync(page, visibleValue, Math.Min(totalTimeoutMs, 4000)); }
                catch { /* si falla, igual seguimos con el loop */ }
            }

            // 2) Preparar TCS / handler de diálogo
            var tcs = new TaskCompletionSource<(string msg, bool ok)>(TaskCreationOptions.RunContinuationsAsynchronously);

            async void Handler(object? sender, IDialog dialog)
            {
                try
                {
                    var msg = dialog.Message ?? string.Empty;
                    await dialog.AcceptAsync();
                    tcs.TrySetResult((msg, true));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    page.Dialog -= Handler;
                }
            }
            page.Dialog += Handler;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int interval = Math.Max(100, Math.Min(minIntervalMs, maxIntervalMs));

            try
            {
                while (sw.ElapsedMilliseconds < totalTimeoutMs)
                {
                    // Buscar el botón como ElementHandle (para poder hacer DblClickAsync real)
                    var handle = await FindActionInputByValueAcrossFramesAsync(
                        page, visibleValue, requireEnabled: false, perAttemptTimeoutMs: 500);

                    if (handle != null)
                    {
                        try
                        {
                            await handle.ScrollIntoViewIfNeededAsync();
                            // Si está deshabilitado, igual probar doble click a veces dispara listeners
                            var enabled = true;
                            try { await handle.WaitForElementStateAsync(ElementState.Enabled, new() { Timeout = 200 }); }
                            catch { enabled = false; }

                            switch (clickMode)
                            {
                                case ClickMode.Single:
                                    await handle.ClickAsync();
                                    break;

                                case ClickMode.Double:
                                    await handle.DblClickAsync();
                                    break;

                                case ClickMode.Both:
                                    // single y luego double en la misma iteración
                                    await handle.ClickAsync();
                                    await page.WaitForTimeoutAsync(60);
                                    await handle.DblClickAsync();
                                    break;
                            }
                        }
                        catch
                        {
                            // Si falla el click por reflow, seguimos en lazo
                        }
                    }
                    else
                    {
                        // Fallback: intento de click rápido vía JS (no doble)
                        _ = await TryClickInputByValueOnceAsync(page, visibleValue);
                    }

                    // Esperar o salir si apareció el diálogo
                    var remaining = (int)Math.Max(0, totalTimeoutMs - sw.ElapsedMilliseconds);
                    var delay = Math.Min(interval, remaining);
                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(delay));
                    if (completed == tcs.Task)
                    {
                        var res = await tcs.Task;
                        if (res.ok) return res.msg;
                        throw new Exception("El diálogo apareció pero no pudo ser aceptado.");
                    }

                    // Nudge + backoff
                    try
                    {
                        await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                        await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                    }
                    catch { /* tolerante */ }

                    interval = Math.Min(interval + 150, maxIntervalMs);
                }

                await page.ScreenshotAsync(new() { Path = $"dialog_not_shown_{Sanitize(visibleValue)}.png", FullPage = true });
                throw new TimeoutException($"No apareció el diálogo al clicar '{visibleValue}' dentro de {totalTimeoutMs} ms.");
            }
            finally
            {
                // seguridad
                page.Dialog -= Handler;
            }
        }











        // === Context menu (ExtJS) helpers ===

        public static async Task ClickContextMenuItemByTextAsync(
            IPage page,
            string text,
            int timeoutMs = 8000,
            bool allowContainsFallback = true,
            bool allowDirectActionFallback = true)
        {
            // 1) Intentar encontrar el <a> del menú en document + floating layers
            var a = await FindContextMenuAnchorAcrossFramesAsync(
                page, text, exact: true, visibleOnly: true,
                totalTimeoutMs: timeoutMs, perAttemptTimeoutMs: 500);

            if (a == null && allowContainsFallback)
            {
                a = await FindContextMenuAnchorAcrossFramesAsync(
                    page, text, exact: false, visibleOnly: true,
                    totalTimeoutMs: timeoutMs, perAttemptTimeoutMs: 500);
            }

            // 2) Si lo encontramos, validar que no esté disabled y clicar
            if (a != null)
            {
                // Verificar disabled (className == 'disabled' o this.disabled==true)
                var disabled = await a.EvaluateAsync<bool>(@"(el) => {
            if (el.classList && el.classList.contains('disabled')) return true;
            // algunos skins usan el atributo 'disabled' en <a> (raro, pero lo toleramos)
            return el.hasAttribute('disabled');
        }");
                if (disabled)
                    throw new Exception($"La opción de menú '{text}' está deshabilitada.");

                await a.ScrollIntoViewIfNeededAsync();
                try { await a.WaitForElementStateAsync(ElementState.Visible); } catch { }
                await a.ClickAsync();
                return;
            }

            // 3) Fallback: invocar callActionMethod(...) directamente si se permite
            if (allowDirectActionFallback)
            {
                var ok = await InvokeMenuActionByTextAsync(page, text, timeoutMs: timeoutMs);
                if (ok) return;
            }

            await page.ScreenshotAsync(new() { Path = $"context_menu_item_{Sanitize(text)}_not_found.png", FullPage = true });
            throw new Exception($"No se encontró la opción de menú '{text}' en el context menu (ni por texto, ni por title, ni por id).");
        }


        // Invoca callActionMethod('...') buscando el <a> por texto/title/id. Retorna true si pudo invocar.
        public static async Task<bool> InvokeMenuActionByTextAsync(
            IPage page,
            string text,
            int timeoutMs = 6000)
        {
            var js = @"
    (args) => {
        const norm = s => (s||'').replace(/\u00A0/g,' ').replace(/\s+/g,' ').trim().toLowerCase();
        const wanted = norm(args.text);
        const idNeedle = (args.text || '').replace(/\s+/g,'').toLowerCase(); // 'Release MHE Messages' -> 'releasemhemessages'

        const isVisible = (el) => {
          if (!(el instanceof Element)) return false;
          const st = getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
        };

        const searchRoots = () => {
          const roots = [document];
          const layers = document.querySelectorAll('" + LAYER_QUERY + @"');
          layers.forEach(r => roots.push(r));
          return roots;
        };

        const extractActionId = (a) => {
          const oc = String(a.getAttribute('onclick') || '');
          const m = oc.match(/callActionMethod\\('([^']+)'\\)/);
          return m ? m[1] : null;
        };

        const roots = searchRoots();

        for (const root of roots) {
          // limitar a los menús más típicos
          const scope = root.querySelector('#rmenu, .menuUI8') || root;

          // Recorremos todas las <a>
          const anchors = scope.querySelectorAll('a');
          for (const a of anchors) {
            if (!isVisible(a)) continue;
            if (a.classList && a.classList.contains('disabled')) continue;

            const sp = a.querySelector('span');
            const spanTxt = norm(sp ? sp.textContent : '');
            const titleTxt = norm(a.getAttribute('title'));
            const idTxt = String(a.id || '').toLowerCase();

            const matches = (
                spanTxt === wanted || titleTxt === wanted ||
                idTxt.includes(idNeedle) ||
                (args.allowContains && (spanTxt.includes(wanted) || titleTxt.includes(wanted)))
            );

            if (!matches) continue;

            const actionId = extractActionId(a);
            if (actionId && typeof window.callActionMethod === 'function') {
              window.callActionMethod(actionId);
              return true;
            }
            // Si no hay actionId, devolvemos false para que el host haga click en el handle
          }
        }
        return false;
    }";

            // 1) Intento exacto
            var ok = await page.EvaluateAsync<bool>(js, new { text, allowContains = false });
            if (ok) return true;

            // 2) Fallback contains
            ok = await page.EvaluateAsync<bool>(js, new { text, allowContains = true });
            return ok;
        }


        // Busca el <a> del menú en document + floating layers por <span> text, title o fragmento de id.
        // Devuelve IElementHandle para hacer ClickAsync() con Playwright.
        private static async Task<IElementHandle?> FindContextMenuAnchorAcrossFramesAsync(
            IPage page,
            string text,
            bool exact = true,
            bool visibleOnly = true,
            int totalTimeoutMs = 8000,
            int perAttemptTimeoutMs = 500)
        {
            var js = @"
      (args) => {
        const norm = s => (s||'').replace(/\u00A0/g,' ').replace(/\s+/g,' ').trim().toLowerCase();
        const wanted = norm(args.text);
        const idNeedle = (args.text || '').replace(/\s+/g,'').toLowerCase();
        const exact = !!args.exact;

        const isVisible = (el) => {
          if (!(el instanceof Element)) return false;
          const st = getComputedStyle(el);
          const rc = el.getBoundingClientRect();
          return st && st.display !== 'none' && st.visibility !== 'hidden' && rc.width > 0 && rc.height > 0;
        };

        const roots = [document];
        for (const lay of document.querySelectorAll('" + LAYER_QUERY + @"')) roots.push(lay);

        for (const root of roots) {
          // context menu containers típicos
          const scope = root.querySelector('#rmenu, .menuUI8') || root;
          const anchors = scope.querySelectorAll('a');

          for (const a of anchors) {
            if (" + (visibleOnly ? " !isVisible(a) " : " false ") + @") continue;
            if (a.classList && a.classList.contains('disabled')) continue;

            const sp = a.querySelector('span');
            const spanTxt = norm(sp ? sp.textContent : '');
            const titleTxt = norm(a.getAttribute('title'));
            const idTxt = String(a.id || '').toLowerCase();

            let ok = false;

            if (exact) {
              ok = (spanTxt === wanted) || (titleTxt === wanted) || idTxt.includes(idNeedle);
            } else {
              ok = (spanTxt.includes(wanted)) || (titleTxt.includes(wanted)) || idTxt.includes(idNeedle);
            }

            if (ok) return a;
          }
        }
        return null;
      }";

            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < totalTimeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        var handle = await frame.WaitForFunctionAsync(js,
                            new { text, exact },
                            new() { Timeout = perAttemptTimeoutMs });

                        var el = AsElementOrNull(handle);
                        if (el != null) return el;
                    }
                    catch { /* probar siguiente frame */ }
                }

                // Nudges de UI
                await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                await Task.Delay(150);
            }

            return null;
        }




















        public static async Task<string> ClickContextMenuItemUntilDialogAndAcceptAsync(
            IPage page,
            string menuText,
            int totalTimeoutMs = 20000,
            int minIntervalMs = 250,
            int maxIntervalMs = 1000,
            ClickMode clickMode = ClickMode.Both,           // Single | Double | Both
            bool allowContainsFallback = false,
            Func<Task>? reopenMenuAsync = null              // Ej: () => RightClickRowByHeaderValueAsync(...)
        )
        {
            // 1) Preparar handler de diálogo
            var tcs = new TaskCompletionSource<(string msg, bool ok)>(TaskCreationOptions.RunContinuationsAsynchronously);
            async void Handler(object? sender, IDialog dialog)
            {
                try
                {
                    var msg = dialog.Message ?? string.Empty;
                    await dialog.AcceptAsync();
                    tcs.TrySetResult((msg, true));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    page.Dialog -= Handler;
                }
            }
            page.Dialog += Handler;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int interval = Math.Max(120, Math.Min(minIntervalMs, maxIntervalMs));
            bool triedReopen = false;

            try
            {
                while (sw.ElapsedMilliseconds < totalTimeoutMs)
                {
                    // 2) Buscar el <a> del menú (document + floating layers)
                    var anchor = await FindContextMenuAnchorAcrossFramesAsync(
                        page, menuText, exact: true, visibleOnly: true, totalTimeoutMs: 800, perAttemptTimeoutMs: 300);

                    if (anchor == null && allowContainsFallback)
                    {
                        anchor = await FindContextMenuAnchorAcrossFramesAsync(
                            page, menuText, exact: false, visibleOnly: true, totalTimeoutMs: 800, perAttemptTimeoutMs: 300);
                    }

                    // Si no lo vemos y tenemos callback para reabrir, lo intentamos una vez por ciclo
                    if (anchor == null && reopenMenuAsync != null && !triedReopen)
                    {
                        try
                        {
                            await reopenMenuAsync();
                            triedReopen = true;
                            // pequeño respiro para que aparezca el overlay
                            await page.WaitForTimeoutAsync(120);
                            // reintenta búsqueda
                            anchor = await FindContextMenuAnchorAcrossFramesAsync(
                                page, menuText, exact: true, visibleOnly: true, totalTimeoutMs: 500, perAttemptTimeoutMs: 250);
                        }
                        catch { /* tolerante */ }
                    }

                    if (anchor != null)
                    {
                        // Verificar disabled
                        var disabled = await anchor.EvaluateAsync<bool>(@"(el) => {
                    if (el.classList && el.classList.contains('disabled')) return true;
                    return el.hasAttribute('disabled');
                }");
                        if (disabled)
                        {
                            await page.ScreenshotAsync(new() { Path = $"menu_item_{Sanitize(menuText)}_disabled.png", FullPage = true });
                            throw new Exception($"La opción de menú '{menuText}' está deshabilitada.");
                        }

                        // 3) Intento de click real (single/double/both)
                        try
                        {
                            await anchor.ScrollIntoViewIfNeededAsync();
                            try { await anchor.WaitForElementStateAsync(ElementState.Visible, new() { Timeout = 200 }); } catch { }

                            switch (clickMode)
                            {
                                case ClickMode.Single:
                                    await anchor.ClickAsync();
                                    break;
                                case ClickMode.Double:
                                    await anchor.DblClickAsync();
                                    break;
                                case ClickMode.Both:
                                    await anchor.ClickAsync();
                                    await page.WaitForTimeoutAsync(60);
                                    await anchor.DblClickAsync();
                                    break;
                            }
                        }
                        catch
                        {
                            // Si el click falla por relayout, probamos fallback callActionMethod directamente
                        }

                        // 4) Fallback: invocar callActionMethod del onclick del <a>
                        try
                        {
                            var invoked = await anchor.EvaluateAsync<bool>(@"(a) => {
                        const oc = String(a.getAttribute('onclick') || '');
                        const m = oc.match(/callActionMethod\('([^']+)'\)/);
                        if (m && typeof window.callActionMethod === 'function') {
                          window.callActionMethod(m[1]);
                          return true;
                        }
                        return false;
                    }");
                            // si invoked==true, dejamos que el diálogo aparezca; si no, igual seguimos
                        }
                        catch { /* tolerante */ }
                    }
                    else
                    {
                        // 5) Si no lo encuentro, intento invocar acción por texto (busca actionId y llama callActionMethod)
                        try
                        {
                            var ok = await InvokeMenuActionByTextAsync(page, menuText, timeoutMs: 800);
                            _ = ok; // aunque sea true/false, esperamos el diálogo abajo
                        }
                        catch { /* tolerante */ }
                    }

                    // 6) Esperar o salir si apareció el diálogo
                    var remaining = (int)Math.Max(0, totalTimeoutMs - sw.ElapsedMilliseconds);
                    var delay = Math.Min(interval, remaining);
                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(delay));
                    if (completed == tcs.Task)
                    {
                        var res = await tcs.Task;
                        if (res.ok) return res.msg;
                        throw new Exception("El diálogo apareció pero no pudo ser aceptado.");
                    }

                    // 7) Nudge + backoff suave; reset intento de reabrir para el próximo ciclo
                    try
                    {
                        await page.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))");
                        await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
                    }
                    catch { /* tolerante */ }

                    triedReopen = false;
                    interval = Math.Min(interval + 150, maxIntervalMs);
                }

                await page.ScreenshotAsync(new() { Path = $"menu_dialog_not_shown_{Sanitize(menuText)}.png", FullPage = true });
                throw new TimeoutException($"No apareció el diálogo al intentar '{menuText}' dentro de {totalTimeoutMs} ms.");
            }
            finally
            {
                page.Dialog -= Handler;
            }
        }




        public static async Task ClickTabByTitleAsync( IPage page, string title, int timeoutMs = 8000)
        {
            var js = @"
    (wanted) => {
        const norm = s => (s || '')
            .replace(/\u00A0/g,' ')
            .replace(/\s+/g,' ')
            .trim()
            .toLowerCase();

        const target = norm(wanted);

        const tabs = document.querySelectorAll('div[title]');
        for (const tab of tabs) {
            if (norm(tab.getAttribute('title')) === target)
                return tab;
        }

        return null;
    }";

            var handle = await page.WaitForFunctionAsync(
                js,
                title,
                new() { Timeout = timeoutMs });

            var el = handle.AsElement();

            if (el == null)
                throw new Exception($"No se encontró el tab '{title}'.");

            await el.ScrollIntoViewIfNeededAsync();
            await el.ClickAsync();
        }



        public static async Task ClickTabAsync(
    IPage page,
    string tabText,
    int timeoutMs = 15000)
        {
            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        var handle = await frame.WaitForFunctionAsync(@"
                (txt) => {

                    const norm = s =>
                        (s || '')
                        .replace(/\u00A0/g,' ')
                        .replace(/\s+/g,' ')
                        .trim()
                        .toLowerCase();

                    const wanted = norm(txt);

                    const candidates = document.querySelectorAll(
                        'div[title], a, span'
                    );

                    for(const el of candidates)
                    {
                        const title =
                            norm(el.getAttribute?.('title'));

                        const text =
                            norm(el.textContent);

                        if(title === wanted || text === wanted)
                            return el;
                    }

                    return null;
                }
                ", tabText, new() { Timeout = 500 });

                        var el = handle.AsElement();

                        if (el != null)
                        {
                            await el.ScrollIntoViewIfNeededAsync();
                            await el.ClickAsync();
                            return;
                        }
                    }
                    catch { }
                }

                await Task.Delay(200);
            }

            throw new Exception($"No se encontró tab '{tabText}'.");
        }




        public static async Task ClickRowByHeaderValueAsync( IPage page, string tableIdPrefix, string headerText, string targetValue, bool exact = true, int timeoutMs = 12000, int perAttemptTimeoutMs = 700 )
        {
            var cell = await FindCellByHeaderValueAcrossFramesAsync(
                page,
                tableIdPrefix,
                headerText,
                targetValue,
                exact,
                visibleOnly: true,
                totalTimeoutMs: timeoutMs,
                perAttemptTimeoutMs: perAttemptTimeoutMs);

            if (cell == null)
            {
                await page.ScreenshotAsync(new()
                {
                    Path = $"row_click_not_found_{Sanitize(headerText)}_{Sanitize(targetValue)}.png",
                    FullPage = true
                });

                throw new Exception(
                    $"No se encontró la fila donde '{headerText}' {(exact ? "==" : "contiene")} '{targetValue}'.");
            }

            await cell.ScrollIntoViewIfNeededAsync();

            try
            {
                await cell.WaitForElementStateAsync(ElementState.Visible);
            }
            catch { }

            await cell.ClickAsync();
        }




        //public static async Task SelectOptionAndClickAddAsync(
        //    IPage page,
        //    string optionText,
        //    int timeoutMs = 12000)
        //{
        //    var start = DateTime.UtcNow;

        //    while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        //    {
        //        bool selected = false;

        //        foreach (var frame in page.Frames)
        //        {
        //            try
        //            {
        //                selected = await frame.EvaluateAsync<bool>(@"
        //        (wanted) => {

        //            const norm = s =>
        //                (s || '')
        //                .replace(/\u00A0/g,' ')
        //                .replace(/\s+/g,' ')
        //                .trim()
        //                .toLowerCase();

        //            wanted = norm(wanted);

        //            const options =
        //                document.querySelectorAll('option');

        //            for(const opt of options)
        //            {
        //                const txt = norm(opt.textContent);

        //                if(txt !== wanted)
        //                    continue;

        //                opt.selected = true;

        //                const select = opt.closest('select');

        //                if(select)
        //                {
        //                    select.value = opt.value;

        //                    select.dispatchEvent(
        //                        new Event('change', { bubbles:true }));

        //                    select.dispatchEvent(
        //                        new Event('input', { bubbles:true }));

        //                    select.dispatchEvent(
        //                        new Event('blur', { bubbles:true }));
        //                }

        //                return true;
        //            }

        //            return false;
        //        }",
        //                optionText);

        //                if (selected)
        //                    break;
        //            }
        //            catch { }
        //        }

        //        if (selected)
        //        {

        //            await ClickInputByValueAsync(
        //                page,
        //                "Add >",
        //                allowSuffix: false,
        //                timeoutMs: 5000);

        //            return;
        //        }

        //        await Task.Delay(200);
        //    }

        //    throw new Exception($"No se encontró la opción '{optionText}'.");
        //}


        public static async Task SelectPTW1AndClickAddAsync(
    IPage page,
    int timeoutMs = 12000)
        {
            foreach (var frame in page.Frames)
            {
                try
                {
                    var ok = await frame.EvaluateAsync<bool>(@"
            () => {

                const select =
                    document.querySelector('#duallist_SourceListId');

                if (!select)
                    return false;

                const opt =
                    Array.from(select.options)
                        .find(o => o.text.trim() === 'PTW1');

                if (!opt)
                    return false;

                opt.scrollIntoView({
                    block:'center'
                });

                opt.selected = true;

                opt.dispatchEvent(
                    new MouseEvent('click',
                    {
                        bubbles:true
                    }));

                select.dispatchEvent(
                    new Event('change',
                    {
                        bubbles:true
                    }));

                return true;
            }");

                    if (ok)
                    {
                        await ClickInputByValueAsync(
                            page,
                            "Add >",
                            allowSuffix: false,
                            timeoutMs: 5000);

                        return;
                    }
                }
                catch { }
            }

            throw new Exception("No se encontró PTW1.");
        }


    }
}
