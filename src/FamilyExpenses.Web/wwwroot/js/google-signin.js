// "Log ind med Google" via Firebase Authentication. Loaded on every page, but only fetches the
// Firebase SDK (from Google's CDN – the app has no JS build pipeline) when a page has the button,
// so the popup can open straight from the click without being blocked.
const sdkVersion = "10.14.1";
let firebase = null;

function loadFirebase() {
    firebase ??= Promise.all([
        import(`https://www.gstatic.com/firebasejs/${sdkVersion}/firebase-app.js`),
        import(`https://www.gstatic.com/firebasejs/${sdkVersion}/firebase-auth.js`),
    ]);
    return firebase;
}

let auth = null;

async function getFirebaseAuth(button) {
    if (auth) return auth;
    const [{ initializeApp }, authSdk] = await loadFirebase();
    const app = initializeApp({
        apiKey: button.dataset.apiKey,
        authDomain: button.dataset.authDomain,
        projectId: button.dataset.projectId,
    });
    auth = { instance: authSdk.getAuth(app), sdk: authSdk };
    return auth;
}

function prepare() {
    const button = document.querySelector("[data-google-signin]");
    if (button) getFirebaseAuth(button);
}

function message(error) {
    switch (error?.code) {
        case "auth/popup-blocked":
            return "Din browser blokerede login-vinduet. Tillad pop op-vinduer for siden og prøv igen.";
        case "auth/network-request-failed":
            return "Ingen forbindelse. Tjek internettet og prøv igen.";
        default:
            return "Login med Google mislykkedes. Prøv igen.";
    }
}

document.addEventListener("click", async (event) => {
    const button = event.target.closest("[data-google-signin]");
    if (!button) return;

    const form = button.closest("form");
    const status = form.querySelector("[data-google-signin-status]");
    status.textContent = "";
    button.disabled = true;

    try {
        // No await when the SDK is already loaded: Safari only allows the popup straight from the click.
        const { instance, sdk } = auth ?? await getFirebaseAuth(button);
        const provider = new sdk.GoogleAuthProvider();
        // Lets people on a shared phone pick which Google account to use.
        provider.setCustomParameters({ prompt: "select_account" });

        const credential = await sdk.signInWithPopup(instance, provider);
        form.elements.idToken.value = await credential.user.getIdToken();
        // The app's own login cookie is the session; don't keep a second one in Firebase.
        await sdk.signOut(instance);
        form.submit();
    } catch (error) {
        button.disabled = false;
        if (error?.code === "auth/popup-closed-by-user" || error?.code === "auth/cancelled-popup-request") return;
        console.error(error);
        status.textContent = message(error);
    }
});

prepare();
// Blazor's enhanced navigation swaps pages without a reload.
window.Blazor?.addEventListener?.("enhancedload", prepare);
