const ID = /^[A-Za-z0-9_-]+$/ // settings are admin-edited, but they end up inside a <script>

/** GA4 (+ Google Ads) and the Meta pixel, from settings; nothing in dev. */
export function useAnalytics() {
  if (import.meta.dev) return
  const s = useSettings().value
  const ga = s?.ga4_id?.match(ID) && s.ga4_id
  const ads = s?.google_ads_id?.match(ID) && s.google_ads_id
  const fb = s?.fb_pixel_id?.match(ID) && s.fb_pixel_id
  const script: { src?: string; async?: boolean; innerHTML?: string }[] = []
  if (ga) {
    script.push(
      { src: `https://www.googletagmanager.com/gtag/js?id=${ga}`, async: true },
      { innerHTML: `window.dataLayer=window.dataLayer||[];function gtag(){dataLayer.push(arguments)}gtag('js',new Date());gtag('config','${ga}');${ads ? `gtag('config','${ads}');` : ''}` },
    )
  }
  if (fb) {
    script.push({
      innerHTML: `!function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}(window,document,'script','https://connect.facebook.net/en_US/fbevents.js');fbq('init','${fb}');fbq('track','PageView');`,
    })
  }
  if (script.length) useHead({ script })
}
