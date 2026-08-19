import { createPinia } from 'pinia'
import { describe, expect, it } from 'vitest'

import { mount } from '@vue/test-utils'
import App from '../App.vue'
import router from '../router'

describe('App', () => {
  it('renders product navigation and the source-aware dashboard', async () => {
    await router.push('/')
    await router.isReady()

    const wrapper = mount(App, {
      global: {
        plugins: [createPinia(), router],
      },
    })

    expect(wrapper.get('nav').text()).toContain('Repositories')
    expect(wrapper.get('h1').text()).toContain('Plan from evidence')
    expect(wrapper.text()).toContain('1/3')
  })
})
