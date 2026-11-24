<template>
  <div class="min-h-screen flex flex-col">
    <SiteHeader />
    <main class="flex-1">
      <slot />
    </main>
    <SiteFooter />
    <ClientOnly><LazySiteChat /></ClientOnly>
  </div>
</template>
