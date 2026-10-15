// Utilidades de interface do PDV (atalhos de teclado do caixa, foco, vibração no celular).
window.pdv = (function () {
    let handler = null;

    function alvoEditavel(el) {
        if (!el) return false;
        const tag = el.tagName;
        return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || el.isContentEditable;
    }

    return {
        registrarAtalhos: function (dotnetRef) {
            this.removerAtalhos();
            handler = function (e) {
                const tecla = e.key;
                const funcao = /^F([1-9]|1[0-2])$/.test(tecla);
                if (funcao) {
                    e.preventDefault();
                    dotnetRef.invokeMethodAsync('TeclaAtalho', tecla);
                    return;
                }
                if (tecla === 'Escape') {
                    dotnetRef.invokeMethodAsync('TeclaAtalho', tecla);
                    return;
                }
                if (alvoEditavel(e.target) || e.ctrlKey || e.altKey || e.metaKey) return;
                if (/^[0-9]$/.test(tecla) || tecla === 'Enter' || tecla === 'Backspace') {
                    e.preventDefault();
                    dotnetRef.invokeMethodAsync('TeclaAtalho', tecla);
                }
            };
            document.addEventListener('keydown', handler, true);
        },
        removerAtalhos: function () {
            if (handler) {
                document.removeEventListener('keydown', handler, true);
                handler = null;
            }
        },
        focar: function (id, selecionar) {
            setTimeout(function () {
                const el = document.getElementById(id);
                if (el) {
                    el.focus();
                    if (selecionar && el.select) el.select();
                }
            }, 30);
        },
        vibrar: function (ms) {
            if (navigator.vibrate) navigator.vibrate(ms || 30);
        },
        telaCheia: function () {
            const el = document.documentElement;
            if (!document.fullscreenElement && el.requestFullscreen) el.requestFullscreen();
            else if (document.exitFullscreen) document.exitFullscreen();
        },
        rolarParaFim: function (id) {
            const el = document.getElementById(id);
            if (el) el.scrollTop = el.scrollHeight;
        }
    };
})();
