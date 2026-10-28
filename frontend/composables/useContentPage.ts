import type { ContentBlock } from '~/types/api'

/** A back-office page (page.about, page.oplata, …): fetched at page level so a missing key is a real 404. */
export async function useContentPage(key: string, fallbackTitle: string): Promise<ContentBlock> {
  const { data } = await useApiFetch<ContentBlock>(`content:${key}`, `/content/${key}`)
  if (!data.value) throw createError({ statusCode: 404 })
  usePageHead({ title: data.value.title || fallbackTitle, description: excerpt(data.value.bodyHtml) })
  return data.value
}
