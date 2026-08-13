// Funzioni JavaScript chiamate da Blazor tramite IJSRuntime.
//
// Se aggiungi qui una funzione ricordati che deve stare su "window",
// altrimenti Blazor non la trova e ottieni l'errore
// "Could not find 'nome' ('nome' was undefined)".

// Tiene traccia del suono in riproduzione, cosi' cliccando piu' volte
// i versi non si accavallano.
let suonoCorrente = null;

/**
 * Riproduce un file audio. Chiamata da HeroicReturn.PlayRandomRoar().
 * @param {string} percorso es. "sounds/subsong_0115.wav"
 */
window.playSound = function (percorso) {
    try {
        // Ferma il suono precedente prima di partire col nuovo
        if (suonoCorrente) {
            suonoCorrente.pause();
            suonoCorrente.currentTime = 0;
        }

        // Il percorso e' relativo: si risolve sul <base href> della pagina,
        // quindi funziona sia in locale sia sotto /Heroic853SiteV1/
        suonoCorrente = new Audio(percorso);
        suonoCorrente.volume = 0.6;

        // I browser bloccano l'audio non partito da un gesto dell'utente:
        // qui parte da un click, ma la promise va gestita comunque per non
        // lasciare eccezioni non catturate in console.
        suonoCorrente.play().catch(function (err) {
            console.warn('[playSound] riproduzione non consentita: ' + err.message);
        });
    } catch (err) {
        console.warn('[playSound] errore su "' + percorso + '": ' + err.message);
    }
};
