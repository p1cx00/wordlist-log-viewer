using System;
using System.Collections.Generic;

namespace WordlistViewer
{
    internal static partial class Localization
    {
        public static AppLanguage[] SupportedLanguages
        {
            get
            {
                return new AppLanguage[]
                {
                    AppLanguage.English,
                    AppLanguage.Turkish,
                    AppLanguage.Spanish,
                    AppLanguage.French,
                    AppLanguage.German,
                    AppLanguage.Portuguese
                };
            }
        }

        public static string NativeLanguageName(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.Turkish: return "Türkçe";
                case AppLanguage.Spanish: return "Español";
                case AppLanguage.French: return "Français";
                case AppLanguage.German: return "Deutsch";
                case AppLanguage.Portuguese: return "Português (Brasil)";
                default: return "English";
            }
        }

        private static Dictionary<string, string> GetDictionary(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.Turkish: return Turkish;
                case AppLanguage.Spanish: return Spanish;
                case AppLanguage.French: return French;
                case AppLanguage.German: return German;
                case AppLanguage.Portuguese: return Portuguese;
                default: return English;
            }
        }

        private static string GetCultureName(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.Turkish: return "tr-TR";
                case AppLanguage.Spanish: return "es-ES";
                case AppLanguage.French: return "fr-FR";
                case AppLanguage.German: return "de-DE";
                case AppLanguage.Portuguese: return "pt-BR";
                default: return "en-US";
            }
        }

        private static string GetLanguageCode(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.Turkish: return "tr";
                case AppLanguage.Spanish: return "es";
                case AppLanguage.French: return "fr";
                case AppLanguage.German: return "de";
                case AppLanguage.Portuguese: return "pt-BR";
                default: return "en";
            }
        }

        private static AppLanguage ParseLanguageCode(string value)
        {
            if (string.Equals(value, "tr", StringComparison.OrdinalIgnoreCase)) return AppLanguage.Turkish;
            if (string.Equals(value, "es", StringComparison.OrdinalIgnoreCase)) return AppLanguage.Spanish;
            if (string.Equals(value, "fr", StringComparison.OrdinalIgnoreCase)) return AppLanguage.French;
            if (string.Equals(value, "de", StringComparison.OrdinalIgnoreCase)) return AppLanguage.German;
            if (string.Equals(value, "pt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "pt-BR", StringComparison.OrdinalIgnoreCase)) return AppLanguage.Portuguese;
            return AppLanguage.English;
        }

        private static Dictionary<string, string> CopyEnglish()
        {
            return new Dictionary<string, string>(English, StringComparer.Ordinal);
        }

        private static Dictionary<string, string> BuildSpanish()
        {
            Dictionary<string, string> d = CopyEnglish();
            d["header.badge"] = "  LÍNEAS VIRTUALES  •  SOLO LECTURA  •  RAM FIJA  ";
            d["file.none"] = "No hay ningún archivo abierto";
            d["file.drop_hint"] = "Arrastra un archivo aquí o usa el botón Abrir archivo.";
            d["open_file"] = "Abrir archivo…";
            d["history.back"] = "Atrás en el historial de ubicaciones";
            d["history.forward"] = "Adelante en el historial de ubicaciones";
            d["bookmark.off"] = "☆ Marcador";
            d["bookmark.on"] = "★ Marcador";
            d["position"] = "Posición";
            d["go"] = "Ir";
            d["view"] = "Vista";
            d["view.normal"] = "Normal";
            d["view.visible_characters"] = "Caracteres invisibles";
            d["view.hex"] = "Hex";
            d["encoding"] = "Codificación";
            d["encoding.automatic"] = "Automática";
            d["encoding.windows1254"] = "Windows-1254 (turco)";
            d["language"] = "Idioma";
            d["search.exact"] = "Búsqueda exacta";
            d["search.previous"] = "↑ Anterior";
            d["search.next"] = "Siguiente ↓";
            d["search.all"] = "Buscar todo";
            d["search.all.tip"] = "Escanea todo el archivo, cuenta cada coincidencia y muestra todas las posiciones en páginas almacenadas en disco.";
            d["cancel"] = "Cancelar";
            d["go_line"] = "Ir a la línea";
            d["index.create"] = "Crear índice";
            d["index.refresh"] = "Actualizar índice";
            d["index.cancel"] = "Cancelar índice";
            d["file_position"] = "POSICIÓN DEL ARCHIVO";
            d["tab.search_results"] = "Resultados";
            d["tab.bookmarks"] = "Marcadores";
            d["tab.performance"] = "Rendimiento";
            d["results.loaded.zero"] = "Coincidencias en el área cargada: 0";
            d["results.loaded.count"] = "Coincidencias en el área cargada: {0}{1}";
            d["results.local_view"] = "Vista local";
            d["bookmark.jump"] = "Ir a la posición";
            d["delete"] = "Eliminar";
            d["metric.speed"] = "VELOCIDAD";
            d["metric.eta"] = "TIEMPO RESTANTE";
            d["metric.scanned"] = "ESCANEADO";
            d["metric.bottleneck"] = "CUELLO DE BOTELLA";
            d["metric.read_compare"] = "LECTURA / COMPARACIÓN";
            d["metric.read_compare.value"] = "{0:N1} s / {1:N1} s";
            d["metric.index"] = "ÍNDICE DE NAVEGACIÓN";
            d["none"] = "Ninguno";
            d["ready"] = "Listo";
            d["status.position.empty"] = "Posición: —";
            d["status.encoding.empty"] = "Codificación: —";
            d["status.selected_position"] = "Posición seleccionada: {0:N3}%";
            d["dialog.open.title"] = "Abrir un archivo de texto, lista, diccionario o registro";
            d["dialog.open.filter"] = "Archivos de texto, listas y registros|*.txt;*.lst;*.dic;*.wordlist;*.log;*.out;*.trace;*.jsonl;*.ndjson|Todos los archivos|*.*";
            d["error.file_not_found"] = "No se encontró el archivo.";
            d["status.loading_file"] = "Leyendo información y sesión del archivo…";
            d["status.file_open_failed"] = "No se pudo abrir el archivo";
            d["error.file_open_failed"] = "No se pudo abrir el archivo:\r\n\r\n{0}";
            d["status.reading_view"] = "Leyendo del disco la región visible…";
            d["error.read_view"] = "No se pudo leer la región visible";
            d["status.cache_ready"] = "Caché continua lista — {0:N0} líneas, hasta {1} bloques en memoria";
            d["status.prefetch_stopped"] = "Precarga detenida: {0}";
            d["status.search_forward"] = "Buscando hacia delante…";
            d["status.search_backward"] = "Buscando hacia atrás…";
            d["status.searching"] = "Buscando… {0:N1}%  •  {1}/s  •  quedan {2}";
            d["status.no_match_forward"] = "No se encontró ninguna coincidencia antes del final del archivo.";
            d["status.no_match_backward"] = "No se encontró ninguna coincidencia antes del inicio del archivo.";
            d["status.match_found"] = "Coincidencia encontrada — byte {0:N0}";
            d["status.search_cancelled"] = "Búsqueda cancelada.";
            d["error.search"] = "No se pudo completar la búsqueda";
            d["results.scan_initial"] = "Escaneando todo el archivo • 0 coincidencias";
            d["results.scan_running"] = "Escaneo en curso";
            d["status.scan_all_start"] = "Buscando coincidencias exactas en todo el archivo…";
            d["metric.scanned_matches"] = "ESCANEADO\r\n{0} / {1}  ({2:N1}%)\r\n{3:N0} coincidencias";
            d["results.scan_progress"] = "Escaneando todo el archivo • {0:N0} coincidencias";
            d["status.scan_progress"] = "Escaneando todo el archivo… {0:N1}%  •  {1:N0} coincidencias  •  {2}/s  •  quedan {3}";
            d["error.source_changed"] = "El archivo de origen cambió durante el escaneo. Los resultados no se guardaron porque ya no son fiables.";
            d["status.scan_complete"] = "Escaneo completo — {0:N0} coincidencias • índice de resultados {1}";
            d["status.scan_cancelled"] = "Se canceló el escaneo completo; se eliminaron los resultados incompletos.";
            d["error.scan_all"] = "No se pudo completar el escaneo del archivo";
            d["error.results_read"] = "No se pudieron leer los resultados";
            d["results.full"] = "Archivo completo: {0:N0} coincidencias\r\nMostrando: {1:N0}–{2:N0}";
            d["status.result"] = "Resultado n.º {0:N0} — byte {1:N0}";
            d["status.view"] = "Vista: {0}";
            d["status.encoding"] = "Codificación: {0}  •  {1}";
            d["status.encoding_changing"] = "Cambiando la codificación…";
            d["error.encoding_change"] = "No se pudo cambiar la codificación";
            d["status.index_building"] = "Creando el índice de navegación por líneas…";
            d["metric.index_counted"] = "ÍNDICE DE NAVEGACIÓN\r\n{0:N0} líneas contadas";
            d["status.index_progress"] = "Creando índice… {0:N1}%  •  {1:N0} líneas  •  quedan {2}";
            d["status.index_ready"] = "Índice listo — {0:N0} líneas en total";
            d["status.index_cancelled"] = "Creación del índice cancelada.";
            d["error.index_build"] = "No se pudo crear el índice";
            d["status.finding_line"] = "Buscando la posición de la línea {0:N0}…";
            d["error.go_line"] = "No se pudo ir a la línea";
            d["status.bookmark_removed"] = "Marcador eliminado — byte {0:N0}";
            d["status.bookmark_added"] = "Marcador añadido — byte {0:N0}";
            d["status.ready_loaded"] = "Listo — {0:N0} líneas en memoria  •  última lectura {1:N0} ms";
            d["status.position"] = "Posición: {0} / {1}  ({2:N3}%)";
            d["status.byte_length"] = "Byte: {0:N0}  •  Longitud: {1:N0}";
            d["index.available"] = "ÍNDICE DE NAVEGACIÓN\r\nListo  •  {0:N0} líneas en total";
            d["index.unavailable"] = "ÍNDICE DE NAVEGACIÓN\r\nNinguno — los números corresponden al área cargada";
            d["status.font_size"] = "Tamaño de fuente: {0:0.#} pt";
            d["duration.days"] = "{0:N0} días {1:hh\\:mm\\:ss}";
            d["unit.per_second"] = "/s";
            d["bottleneck.mixed"] = "Mixto";
            d["fatal.unexpected"] = "Se produjo un error inesperado:\r\n\r\n{0}";
            d["viewer.copy"] = "Copiar líneas seleccionadas";
            d["viewer.copy_info"] = "Copiar con información de bytes";
            d["viewer.toggle_bookmark"] = "Alternar marcador";
            d["viewer.open_line"] = "Abrir línea por separado";
            d["viewer.header.line"] = "LÍNEA";
            d["viewer.header.offset"] = "POSICIÓN BYTE";
            d["viewer.header.length"] = "LONGITUD";
            d["viewer.header.text"] = "TEXTO";
            d["long_line.title"] = "Línea larga / Segmento sin procesar";
            d["long_line.previous"] = "← Segmento anterior";
            d["long_line.next"] = "Segmento siguiente →";
            d["long_line.copy"] = "Copiar selección";
            d["long_line.reading"] = "Leyendo segmento…";
            d["long_line.info"] = "Byte {0:N0}–{1:N0}  •  {2:N0} bytes{3}";
            d["long_line.continues"] = "  •  la línea continúa";
            d["long_line.error"] = "No se pudo leer el segmento:\r\n\r\n{0}";
            d["error.index_source_changed"] = "El archivo cambió mientras se creaba el índice; no se guardó el índice.";
            d["error.result_disk_space"] = "No queda espacio seguro suficiente para los resultados. El escaneo se detuvo para mantener al menos 2 GB libres en la unidad del sistema.";
            d["error.session_invalid"] = "No hay una sesión de archivo válida para guardar.";
            d["language.changed"] = "El idioma cambió a español.";
            return d;
        }

        private static Dictionary<string, string> BuildFrench()
        {
            Dictionary<string, string> d = CopyEnglish();
            d["header.badge"] = "  LIGNES VIRTUELLES  •  LECTURE SEULE  •  RAM FIXE  ";
            d["file.none"] = "Aucun fichier ouvert";
            d["file.drop_hint"] = "Déposez un fichier ici ou utilisez le bouton Ouvrir un fichier.";
            d["open_file"] = "Ouvrir un fichier…";
            d["history.back"] = "Reculer dans l’historique des positions";
            d["history.forward"] = "Avancer dans l’historique des positions";
            d["bookmark.off"] = "☆ Signet";
            d["bookmark.on"] = "★ Signet";
            d["position"] = "Position";
            d["go"] = "Aller";
            d["view"] = "Affichage";
            d["view.normal"] = "Normal";
            d["view.visible_characters"] = "Caractères invisibles";
            d["view.hex"] = "Hex";
            d["encoding"] = "Encodage";
            d["encoding.automatic"] = "Automatique";
            d["encoding.windows1254"] = "Windows-1254 (turc)";
            d["language"] = "Langue";
            d["search.exact"] = "Recherche exacte";
            d["search.previous"] = "↑ Précédent";
            d["search.next"] = "Suivant ↓";
            d["search.all"] = "Tout rechercher";
            d["search.all.tip"] = "Analyse tout le fichier, compte chaque correspondance et affiche toutes les positions dans des pages stockées sur disque.";
            d["cancel"] = "Annuler";
            d["go_line"] = "Aller à la ligne";
            d["index.create"] = "Créer l’index";
            d["index.refresh"] = "Actualiser l’index";
            d["index.cancel"] = "Annuler l’index";
            d["file_position"] = "POSITION DANS LE FICHIER";
            d["tab.search_results"] = "Résultats";
            d["tab.bookmarks"] = "Signets";
            d["tab.performance"] = "Performances";
            d["results.loaded.zero"] = "Correspondances dans la zone chargée : 0";
            d["results.loaded.count"] = "Correspondances dans la zone chargée : {0}{1}";
            d["results.local_view"] = "Vue locale";
            d["bookmark.jump"] = "Aller à la position";
            d["delete"] = "Supprimer";
            d["metric.speed"] = "VITESSE";
            d["metric.eta"] = "TEMPS RESTANT";
            d["metric.scanned"] = "ANALYSÉ";
            d["metric.bottleneck"] = "GOULOT D’ÉTRANGLEMENT";
            d["metric.read_compare"] = "LECTURE / COMPARAISON";
            d["metric.read_compare.value"] = "{0:N1} s / {1:N1} s";
            d["metric.index"] = "INDEX DE NAVIGATION";
            d["none"] = "Aucun";
            d["ready"] = "Prêt";
            d["status.position.empty"] = "Position : —";
            d["status.encoding.empty"] = "Encodage : —";
            d["status.selected_position"] = "Position sélectionnée : {0:N3} %";
            d["dialog.open.title"] = "Ouvrir un fichier texte, une liste, un dictionnaire ou un journal";
            d["dialog.open.filter"] = "Fichiers texte, listes et journaux|*.txt;*.lst;*.dic;*.wordlist;*.log;*.out;*.trace;*.jsonl;*.ndjson|Tous les fichiers|*.*";
            d["error.file_not_found"] = "Fichier introuvable.";
            d["status.loading_file"] = "Lecture des informations et de la session du fichier…";
            d["status.file_open_failed"] = "Impossible d’ouvrir le fichier";
            d["error.file_open_failed"] = "Impossible d’ouvrir le fichier :\r\n\r\n{0}";
            d["status.reading_view"] = "Lecture de la zone visible depuis le disque…";
            d["error.read_view"] = "Impossible de lire la zone visible";
            d["status.cache_ready"] = "Cache continu prêt — {0:N0} lignes, jusqu’à {1} blocs en mémoire";
            d["status.prefetch_stopped"] = "Préchargement arrêté : {0}";
            d["status.search_forward"] = "Recherche vers l’avant…";
            d["status.search_backward"] = "Recherche vers l’arrière…";
            d["status.searching"] = "Recherche… {0:N1} %  •  {1}/s  •  reste {2}";
            d["status.no_match_forward"] = "Aucune correspondance trouvée avant la fin du fichier.";
            d["status.no_match_backward"] = "Aucune correspondance trouvée avant le début du fichier.";
            d["status.match_found"] = "Correspondance trouvée — octet {0:N0}";
            d["status.search_cancelled"] = "Recherche annulée.";
            d["error.search"] = "Impossible de terminer la recherche";
            d["results.scan_initial"] = "Analyse de tout le fichier • 0 correspondance";
            d["results.scan_running"] = "Analyse en cours";
            d["status.scan_all_start"] = "Recherche des correspondances exactes dans tout le fichier…";
            d["metric.scanned_matches"] = "ANALYSÉ\r\n{0} / {1}  ({2:N1} %)\r\n{3:N0} correspondances";
            d["results.scan_progress"] = "Analyse de tout le fichier • {0:N0} correspondances";
            d["status.scan_progress"] = "Analyse du fichier… {0:N1} %  •  {1:N0} correspondances  •  {2}/s  •  reste {3}";
            d["error.source_changed"] = "Le fichier source a changé pendant l’analyse. Les résultats n’ont pas été enregistrés car ils ne sont plus fiables.";
            d["status.scan_complete"] = "Analyse terminée — {0:N0} correspondances • index des résultats {1}";
            d["status.scan_cancelled"] = "Analyse complète annulée ; les résultats incomplets ont été supprimés.";
            d["error.scan_all"] = "Impossible de terminer l’analyse du fichier";
            d["error.results_read"] = "Impossible de lire les résultats";
            d["results.full"] = "Fichier entier : {0:N0} correspondances\r\nAffichage : {1:N0}–{2:N0}";
            d["status.result"] = "Résultat n° {0:N0} — octet {1:N0}";
            d["status.view"] = "Affichage : {0}";
            d["status.encoding"] = "Encodage : {0}  •  {1}";
            d["status.encoding_changing"] = "Modification de l’encodage…";
            d["error.encoding_change"] = "Impossible de modifier l’encodage";
            d["status.index_building"] = "Création de l’index de navigation par ligne…";
            d["metric.index_counted"] = "INDEX DE NAVIGATION\r\n{0:N0} lignes comptées";
            d["status.index_progress"] = "Création de l’index… {0:N1} %  •  {1:N0} lignes  •  reste {2}";
            d["status.index_ready"] = "Index prêt — {0:N0} lignes au total";
            d["status.index_cancelled"] = "Création de l’index annulée.";
            d["error.index_build"] = "Impossible de créer l’index";
            d["status.finding_line"] = "Recherche de la position de la ligne {0:N0}…";
            d["error.go_line"] = "Impossible d’accéder à la ligne";
            d["status.bookmark_removed"] = "Signet supprimé — octet {0:N0}";
            d["status.bookmark_added"] = "Signet ajouté — octet {0:N0}";
            d["status.ready_loaded"] = "Prêt — {0:N0} lignes en mémoire  •  dernière lecture {1:N0} ms";
            d["status.position"] = "Position : {0} / {1}  ({2:N3} %)";
            d["status.byte_length"] = "Octet : {0:N0}  •  Longueur : {1:N0}";
            d["index.available"] = "INDEX DE NAVIGATION\r\nPrêt  •  {0:N0} lignes au total";
            d["index.unavailable"] = "INDEX DE NAVIGATION\r\nAucun — les numéros concernent la zone chargée";
            d["status.font_size"] = "Taille de police : {0:0.#} pt";
            d["duration.days"] = "{0:N0} jours {1:hh\\:mm\\:ss}";
            d["unit.per_second"] = "/s";
            d["bottleneck.mixed"] = "Mixte";
            d["fatal.unexpected"] = "Une erreur inattendue s’est produite :\r\n\r\n{0}";
            d["viewer.copy"] = "Copier les lignes sélectionnées";
            d["viewer.copy_info"] = "Copier avec les informations d’octet";
            d["viewer.toggle_bookmark"] = "Activer/désactiver le signet";
            d["viewer.open_line"] = "Ouvrir la ligne séparément";
            d["viewer.header.line"] = "LIGNE";
            d["viewer.header.offset"] = "POSITION OCTET";
            d["viewer.header.length"] = "LONGUEUR";
            d["viewer.header.text"] = "TEXTE";
            d["long_line.title"] = "Ligne longue / Segment brut";
            d["long_line.previous"] = "← Segment précédent";
            d["long_line.next"] = "Segment suivant →";
            d["long_line.copy"] = "Copier la sélection";
            d["long_line.reading"] = "Lecture du segment…";
            d["long_line.info"] = "Octets {0:N0}–{1:N0}  •  {2:N0} octets{3}";
            d["long_line.continues"] = "  •  la ligne continue";
            d["long_line.error"] = "Impossible de lire le segment :\r\n\r\n{0}";
            d["error.index_source_changed"] = "Le fichier a changé pendant la création de l’index ; l’index n’a pas été enregistré.";
            d["error.result_disk_space"] = "L’espace disque sûr est insuffisant pour les résultats. L’analyse a été arrêtée afin de conserver au moins 2 Go libres sur le disque système.";
            d["error.session_invalid"] = "Aucune session de fichier valide à enregistrer.";
            d["language.changed"] = "La langue a été définie sur le français.";
            return d;
        }

        private static Dictionary<string, string> BuildGerman()
        {
            Dictionary<string, string> d = CopyEnglish();
            d["header.badge"] = "  VIRTUELLE ZEILEN  •  SCHREIBGESCHÜTZT  •  FESTER RAM  ";
            d["file.none"] = "Keine Datei geöffnet";
            d["file.drop_hint"] = "Datei hier ablegen oder die Schaltfläche Datei öffnen verwenden.";
            d["open_file"] = "Datei öffnen…";
            d["history.back"] = "Im Positionsverlauf zurück";
            d["history.forward"] = "Im Positionsverlauf vor";
            d["bookmark.off"] = "☆ Lesezeichen";
            d["bookmark.on"] = "★ Lesezeichen";
            d["position"] = "Position";
            d["go"] = "Gehe zu";
            d["view"] = "Ansicht";
            d["view.normal"] = "Normal";
            d["view.visible_characters"] = "Unsichtbare Zeichen";
            d["view.hex"] = "Hex";
            d["encoding"] = "Codierung";
            d["encoding.automatic"] = "Automatisch";
            d["encoding.windows1254"] = "Windows-1254 (Türkisch)";
            d["language"] = "Sprache";
            d["search.exact"] = "Exakte Suche";
            d["search.previous"] = "↑ Vorheriger";
            d["search.next"] = "Nächster ↓";
            d["search.all"] = "Alle suchen";
            d["search.all.tip"] = "Durchsucht die gesamte Datei, zählt jeden Treffer und zeigt alle Positionen in datenträgergestützten Seiten an.";
            d["cancel"] = "Abbrechen";
            d["go_line"] = "Gehe zu Zeile";
            d["index.create"] = "Index erstellen";
            d["index.refresh"] = "Index aktualisieren";
            d["index.cancel"] = "Index abbrechen";
            d["file_position"] = "DATEIPOSITION";
            d["tab.search_results"] = "Suchergebnisse";
            d["tab.bookmarks"] = "Lesezeichen";
            d["tab.performance"] = "Leistung";
            d["results.loaded.zero"] = "Treffer im geladenen Bereich: 0";
            d["results.loaded.count"] = "Treffer im geladenen Bereich: {0}{1}";
            d["results.local_view"] = "Lokale Ansicht";
            d["bookmark.jump"] = "Zur Position";
            d["delete"] = "Löschen";
            d["metric.speed"] = "GESCHWINDIGKEIT";
            d["metric.eta"] = "RESTZEIT";
            d["metric.scanned"] = "DURCHSUCHT";
            d["metric.bottleneck"] = "ENGPASS";
            d["metric.read_compare"] = "LESEN / VERGLEICHEN";
            d["metric.read_compare.value"] = "{0:N1} s / {1:N1} s";
            d["metric.index"] = "NAVIGATIONSINDEX";
            d["none"] = "Keiner";
            d["ready"] = "Bereit";
            d["status.position.empty"] = "Position: —";
            d["status.encoding.empty"] = "Codierung: —";
            d["status.selected_position"] = "Ausgewählte Position: {0:N3} %";
            d["dialog.open.title"] = "Text-, Wortlisten-, Wörterbuch- oder Protokolldatei öffnen";
            d["dialog.open.filter"] = "Text-, Wortlisten- und Protokolldateien|*.txt;*.lst;*.dic;*.wordlist;*.log;*.out;*.trace;*.jsonl;*.ndjson|Alle Dateien|*.*";
            d["error.file_not_found"] = "Datei nicht gefunden.";
            d["status.loading_file"] = "Dateiinformationen und Sitzung werden gelesen…";
            d["status.file_open_failed"] = "Datei konnte nicht geöffnet werden";
            d["error.file_open_failed"] = "Datei konnte nicht geöffnet werden:\r\n\r\n{0}";
            d["status.reading_view"] = "Sichtbarer Bereich wird vom Datenträger gelesen…";
            d["error.read_view"] = "Sichtbarer Bereich konnte nicht gelesen werden";
            d["status.cache_ready"] = "Fortlaufender Cache bereit — {0:N0} Zeilen, bis zu {1} Blöcke im Speicher";
            d["status.prefetch_stopped"] = "Vorladen angehalten: {0}";
            d["status.search_forward"] = "Vorwärts suchen…";
            d["status.search_backward"] = "Rückwärts suchen…";
            d["status.searching"] = "Suche… {0:N1} %  •  {1}/s  •  noch {2}";
            d["status.no_match_forward"] = "Bis zum Dateiende wurde kein Treffer gefunden.";
            d["status.no_match_backward"] = "Bis zum Dateianfang wurde kein Treffer gefunden.";
            d["status.match_found"] = "Treffer gefunden — Byte {0:N0}";
            d["status.search_cancelled"] = "Suche abgebrochen.";
            d["error.search"] = "Suche konnte nicht abgeschlossen werden";
            d["results.scan_initial"] = "Gesamte Datei wird durchsucht • 0 Treffer";
            d["results.scan_running"] = "Suche läuft";
            d["status.scan_all_start"] = "Exakte Treffer werden in der gesamten Datei gesucht…";
            d["metric.scanned_matches"] = "DURCHSUCHT\r\n{0} / {1}  ({2:N1} %)\r\n{3:N0} Treffer";
            d["results.scan_progress"] = "Gesamte Datei wird durchsucht • {0:N0} Treffer";
            d["status.scan_progress"] = "Datei wird durchsucht… {0:N1} %  •  {1:N0} Treffer  •  {2}/s  •  noch {3}";
            d["error.source_changed"] = "Die Quelldatei wurde während der Suche geändert. Die Ergebnisse wurden nicht gespeichert, da sie nicht mehr zuverlässig sind.";
            d["status.scan_complete"] = "Vollständige Suche beendet — {0:N0} Treffer • Ergebnisindex {1}";
            d["status.scan_cancelled"] = "Vollständige Suche abgebrochen; unvollständige Ergebnisse wurden gelöscht.";
            d["error.scan_all"] = "Vollständige Dateisuche konnte nicht abgeschlossen werden";
            d["error.results_read"] = "Suchergebnisse konnten nicht gelesen werden";
            d["results.full"] = "Gesamte Datei: {0:N0} Treffer\r\nAngezeigt: {1:N0}–{2:N0}";
            d["status.result"] = "Ergebnis Nr. {0:N0} — Byte {1:N0}";
            d["status.view"] = "Ansicht: {0}";
            d["status.encoding"] = "Codierung: {0}  •  {1}";
            d["status.encoding_changing"] = "Codierung wird geändert…";
            d["error.encoding_change"] = "Codierung konnte nicht geändert werden";
            d["status.index_building"] = "Zeilennavigationsindex wird erstellt…";
            d["metric.index_counted"] = "NAVIGATIONSINDEX\r\n{0:N0} Zeilen gezählt";
            d["status.index_progress"] = "Index wird erstellt… {0:N1} %  •  {1:N0} Zeilen  •  noch {2}";
            d["status.index_ready"] = "Navigationsindex bereit — insgesamt {0:N0} Zeilen";
            d["status.index_cancelled"] = "Indexerstellung abgebrochen.";
            d["error.index_build"] = "Index konnte nicht erstellt werden";
            d["status.finding_line"] = "Position von Zeile {0:N0} wird gesucht…";
            d["error.go_line"] = "Zeile konnte nicht aufgerufen werden";
            d["status.bookmark_removed"] = "Lesezeichen entfernt — Byte {0:N0}";
            d["status.bookmark_added"] = "Lesezeichen hinzugefügt — Byte {0:N0}";
            d["status.ready_loaded"] = "Bereit — {0:N0} Zeilen im Speicher  •  letzter Lesevorgang {1:N0} ms";
            d["status.position"] = "Position: {0} / {1}  ({2:N3} %)";
            d["status.byte_length"] = "Byte: {0:N0}  •  Länge: {1:N0}";
            d["index.available"] = "NAVIGATIONSINDEX\r\nBereit  •  insgesamt {0:N0} Zeilen";
            d["index.unavailable"] = "NAVIGATIONSINDEX\r\nKeiner — Zeilennummern beziehen sich auf den geladenen Bereich";
            d["status.font_size"] = "Schriftgröße: {0:0.#} pt";
            d["duration.days"] = "{0:N0} Tage {1:hh\\:mm\\:ss}";
            d["unit.per_second"] = "/s";
            d["bottleneck.mixed"] = "Gemischt";
            d["fatal.unexpected"] = "Ein unerwarteter Fehler ist aufgetreten:\r\n\r\n{0}";
            d["viewer.copy"] = "Ausgewählte Zeilen kopieren";
            d["viewer.copy_info"] = "Mit Byte-Informationen kopieren";
            d["viewer.toggle_bookmark"] = "Lesezeichen umschalten";
            d["viewer.open_line"] = "Zeile separat öffnen";
            d["viewer.header.line"] = "ZEILE";
            d["viewer.header.offset"] = "BYTE-POSITION";
            d["viewer.header.length"] = "LÄNGE";
            d["viewer.header.text"] = "TEXT";
            d["long_line.title"] = "Lange Zeile / Rohsegment";
            d["long_line.previous"] = "← Vorheriges Segment";
            d["long_line.next"] = "Nächstes Segment →";
            d["long_line.copy"] = "Auswahl kopieren";
            d["long_line.reading"] = "Segment wird gelesen…";
            d["long_line.info"] = "Byte {0:N0}–{1:N0}  •  {2:N0} Bytes{3}";
            d["long_line.continues"] = "  •  Zeile wird fortgesetzt";
            d["long_line.error"] = "Segment konnte nicht gelesen werden:\r\n\r\n{0}";
            d["error.index_source_changed"] = "Die Datei wurde während der Indexerstellung geändert; der Index wurde nicht gespeichert.";
            d["error.result_disk_space"] = "Für Suchergebnisse ist nicht genügend sicherer Speicherplatz verfügbar. Die Suche wurde beendet, um mindestens 2 GB auf dem Systemlaufwerk freizuhalten.";
            d["error.session_invalid"] = "Es gibt keine gültige Dateisitzung zum Speichern.";
            d["language.changed"] = "Die Sprache wurde auf Deutsch geändert.";
            return d;
        }

        private static Dictionary<string, string> BuildPortuguese()
        {
            Dictionary<string, string> d = CopyEnglish();
            d["header.badge"] = "  LINHAS VIRTUAIS  •  SOMENTE LEITURA  •  RAM FIXA  ";
            d["file.none"] = "Nenhum arquivo aberto";
            d["file.drop_hint"] = "Arraste um arquivo para cá ou use o botão Abrir arquivo.";
            d["open_file"] = "Abrir arquivo…";
            d["history.back"] = "Voltar no histórico de posições";
            d["history.forward"] = "Avançar no histórico de posições";
            d["bookmark.off"] = "☆ Marcador";
            d["bookmark.on"] = "★ Marcador";
            d["position"] = "Posição";
            d["go"] = "Ir";
            d["view"] = "Exibição";
            d["view.normal"] = "Normal";
            d["view.visible_characters"] = "Caracteres invisíveis";
            d["view.hex"] = "Hex";
            d["encoding"] = "Codificação";
            d["encoding.automatic"] = "Automática";
            d["encoding.windows1254"] = "Windows-1254 (turco)";
            d["language"] = "Idioma";
            d["search.exact"] = "Busca exata";
            d["search.previous"] = "↑ Anterior";
            d["search.next"] = "Próximo ↓";
            d["search.all"] = "Buscar tudo";
            d["search.all.tip"] = "Examina o arquivo inteiro, conta cada ocorrência e mostra todas as posições em páginas armazenadas no disco.";
            d["cancel"] = "Cancelar";
            d["go_line"] = "Ir para a linha";
            d["index.create"] = "Criar índice";
            d["index.refresh"] = "Atualizar índice";
            d["index.cancel"] = "Cancelar índice";
            d["file_position"] = "POSIÇÃO NO ARQUIVO";
            d["tab.search_results"] = "Resultados";
            d["tab.bookmarks"] = "Marcadores";
            d["tab.performance"] = "Desempenho";
            d["results.loaded.zero"] = "Ocorrências na área carregada: 0";
            d["results.loaded.count"] = "Ocorrências na área carregada: {0}{1}";
            d["results.local_view"] = "Exibição local";
            d["bookmark.jump"] = "Ir para a posição";
            d["delete"] = "Excluir";
            d["metric.speed"] = "VELOCIDADE";
            d["metric.eta"] = "TEMPO RESTANTE";
            d["metric.scanned"] = "EXAMINADO";
            d["metric.bottleneck"] = "GARGALO";
            d["metric.read_compare"] = "LEITURA / COMPARAÇÃO";
            d["metric.read_compare.value"] = "{0:N1} s / {1:N1} s";
            d["metric.index"] = "ÍNDICE DE NAVEGAÇÃO";
            d["none"] = "Nenhum";
            d["ready"] = "Pronto";
            d["status.position.empty"] = "Posição: —";
            d["status.encoding.empty"] = "Codificação: —";
            d["status.selected_position"] = "Posição selecionada: {0:N3}%";
            d["dialog.open.title"] = "Abrir arquivo de texto, lista, dicionário ou log";
            d["dialog.open.filter"] = "Arquivos de texto, listas e logs|*.txt;*.lst;*.dic;*.wordlist;*.log;*.out;*.trace;*.jsonl;*.ndjson|Todos os arquivos|*.*";
            d["error.file_not_found"] = "Arquivo não encontrado.";
            d["status.loading_file"] = "Lendo informações e sessão do arquivo…";
            d["status.file_open_failed"] = "Não foi possível abrir o arquivo";
            d["error.file_open_failed"] = "Não foi possível abrir o arquivo:\r\n\r\n{0}";
            d["status.reading_view"] = "Lendo do disco a região visível…";
            d["error.read_view"] = "Não foi possível ler a região visível";
            d["status.cache_ready"] = "Cache contínuo pronto — {0:N0} linhas, até {1} blocos na memória";
            d["status.prefetch_stopped"] = "Pré-carregamento interrompido: {0}";
            d["status.search_forward"] = "Buscando para frente…";
            d["status.search_backward"] = "Buscando para trás…";
            d["status.searching"] = "Buscando… {0:N1}%  •  {1}/s  •  restam {2}";
            d["status.no_match_forward"] = "Nenhuma ocorrência foi encontrada antes do fim do arquivo.";
            d["status.no_match_backward"] = "Nenhuma ocorrência foi encontrada antes do início do arquivo.";
            d["status.match_found"] = "Ocorrência encontrada — byte {0:N0}";
            d["status.search_cancelled"] = "Busca cancelada.";
            d["error.search"] = "Não foi possível concluir a busca";
            d["results.scan_initial"] = "Examinando o arquivo inteiro • 0 ocorrências";
            d["results.scan_running"] = "Exame em andamento";
            d["status.scan_all_start"] = "Buscando ocorrências exatas no arquivo inteiro…";
            d["metric.scanned_matches"] = "EXAMINADO\r\n{0} / {1}  ({2:N1}%)\r\n{3:N0} ocorrências";
            d["results.scan_progress"] = "Examinando o arquivo inteiro • {0:N0} ocorrências";
            d["status.scan_progress"] = "Examinando o arquivo… {0:N1}%  •  {1:N0} ocorrências  •  {2}/s  •  restam {3}";
            d["error.source_changed"] = "O arquivo de origem mudou durante o exame. Os resultados não foram salvos porque deixaram de ser confiáveis.";
            d["status.scan_complete"] = "Exame completo — {0:N0} ocorrências • índice de resultados {1}";
            d["status.scan_cancelled"] = "O exame completo foi cancelado; os resultados incompletos foram excluídos.";
            d["error.scan_all"] = "Não foi possível concluir o exame do arquivo";
            d["error.results_read"] = "Não foi possível ler os resultados";
            d["results.full"] = "Arquivo inteiro: {0:N0} ocorrências\r\nExibindo: {1:N0}–{2:N0}";
            d["status.result"] = "Resultado nº {0:N0} — byte {1:N0}";
            d["status.view"] = "Exibição: {0}";
            d["status.encoding"] = "Codificação: {0}  •  {1}";
            d["status.encoding_changing"] = "Alterando a codificação…";
            d["error.encoding_change"] = "Não foi possível alterar a codificação";
            d["status.index_building"] = "Criando o índice de navegação por linhas…";
            d["metric.index_counted"] = "ÍNDICE DE NAVEGAÇÃO\r\n{0:N0} linhas contadas";
            d["status.index_progress"] = "Criando índice… {0:N1}%  •  {1:N0} linhas  •  restam {2}";
            d["status.index_ready"] = "Índice pronto — {0:N0} linhas no total";
            d["status.index_cancelled"] = "Criação do índice cancelada.";
            d["error.index_build"] = "Não foi possível criar o índice";
            d["status.finding_line"] = "Localizando a posição da linha {0:N0}…";
            d["error.go_line"] = "Não foi possível ir para a linha";
            d["status.bookmark_removed"] = "Marcador removido — byte {0:N0}";
            d["status.bookmark_added"] = "Marcador adicionado — byte {0:N0}";
            d["status.ready_loaded"] = "Pronto — {0:N0} linhas na memória  •  última leitura {1:N0} ms";
            d["status.position"] = "Posição: {0} / {1}  ({2:N3}%)";
            d["status.byte_length"] = "Byte: {0:N0}  •  Tamanho: {1:N0}";
            d["index.available"] = "ÍNDICE DE NAVEGAÇÃO\r\nPronto  •  {0:N0} linhas no total";
            d["index.unavailable"] = "ÍNDICE DE NAVEGAÇÃO\r\nNenhum — os números pertencem à área carregada";
            d["status.font_size"] = "Tamanho da fonte: {0:0.#} pt";
            d["duration.days"] = "{0:N0} dias {1:hh\\:mm\\:ss}";
            d["unit.per_second"] = "/s";
            d["bottleneck.mixed"] = "Misto";
            d["fatal.unexpected"] = "Ocorreu um erro inesperado:\r\n\r\n{0}";
            d["viewer.copy"] = "Copiar linhas selecionadas";
            d["viewer.copy_info"] = "Copiar com informações de byte";
            d["viewer.toggle_bookmark"] = "Alternar marcador";
            d["viewer.open_line"] = "Abrir linha separadamente";
            d["viewer.header.line"] = "LINHA";
            d["viewer.header.offset"] = "POSIÇÃO BYTE";
            d["viewer.header.length"] = "TAMANHO";
            d["viewer.header.text"] = "TEXTO";
            d["long_line.title"] = "Linha longa / Segmento bruto";
            d["long_line.previous"] = "← Segmento anterior";
            d["long_line.next"] = "Próximo segmento →";
            d["long_line.copy"] = "Copiar seleção";
            d["long_line.reading"] = "Lendo segmento…";
            d["long_line.info"] = "Byte {0:N0}–{1:N0}  •  {2:N0} bytes{3}";
            d["long_line.continues"] = "  •  a linha continua";
            d["long_line.error"] = "Não foi possível ler o segmento:\r\n\r\n{0}";
            d["error.index_source_changed"] = "O arquivo mudou durante a criação do índice; o índice não foi salvo.";
            d["error.result_disk_space"] = "Não há espaço seguro suficiente para os resultados. O exame foi interrompido para manter pelo menos 2 GB livres na unidade do sistema.";
            d["error.session_invalid"] = "Não há uma sessão de arquivo válida para salvar.";
            d["language.changed"] = "O idioma foi alterado para português.";
            return d;
        }
    }
}
