mergeInto(LibraryManager.library, {
  X500V2ExitToLanding: function () {
    try {
      if (typeof window !== "undefined" && window.parent && window.parent !== window) {
        try {
          if (typeof window.parent.requestAppExit === "function") {
            window.parent.requestAppExit();
            return;
          }
          if (typeof window.parent.x500v2ExitApp === "function") {
            window.parent.x500v2ExitApp();
            return;
          }
        } catch (ignoredDirectError) {}

        var message = {
          type: "x500v2:exit-app",
          source: "unity-webgl",
          sentAt: Date.now ? Date.now() : 0
        };

        try {
          window.parent.postMessage(message, "*");
        } catch (ignoredPost1) {}

        try {
          window.parent.postMessage("x500v2:exit-app", "*");
        } catch (ignoredPost2) {}

        window.setTimeout(function () {
          try {
            if (typeof window.parent.requestAppExit === "function") {
              window.parent.requestAppExit();
            } else {
              window.parent.postMessage(message, "*");
            }
          } catch (ignoredRetry) {}
        }, 60);

        return;
      }

      if (typeof window !== "undefined") {
        if (window.history && window.history.length > 1) {
          window.history.back();
          return;
        }
        if (window.location) {
          window.location.href = "../";
          return;
        }
      }
    } catch (exitError) {
      try {
        if (window.history && window.history.length > 1) {
          window.history.back();
          return;
        }
        if (window.location) {
          window.location.href = "../";
          return;
        }
      } catch (ignoredExitError) {}
    }
  },

  X500V2ReportBrowserBackResult: function (handled) {
    try {
      if (typeof window !== "undefined" && window.parent && window.parent !== window) {
        window.parent.postMessage(
          { type: "x500v2:browser-back-result", handled: handled !== 0 },
          "*"
        );
      }
    } catch (ignoredBackResultError) {}
  },

  X500V2DownloadTextFile: function (fileNamePtr, mimeTypePtr, contentPtr) {
    try {
      var fileName = UTF8ToString(fileNamePtr) || "x500v2_export.txt";
      var mimeType = UTF8ToString(mimeTypePtr) || "text/plain;charset=utf-8";
      var content = UTF8ToString(contentPtr) || "";

      if (typeof window === "undefined" || typeof document === "undefined") {
        return;
      }

      var blob = new Blob([content], { type: mimeType });
      var url = window.URL.createObjectURL(blob);
      var link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      link.style.display = "none";
      document.body.appendChild(link);
      link.click();

      window.setTimeout(function () {
        try {
          document.body.removeChild(link);
        } catch (ignoredRemoveError) {}
        try {
          window.URL.revokeObjectURL(url);
        } catch (ignoredRevokeError) {}
      }, 500);
    } catch (downloadError) {
      try {
        console.error("[X500V2] Could not download profiler export", downloadError);
      } catch (ignoredConsoleError) {}
    }
  }
});
