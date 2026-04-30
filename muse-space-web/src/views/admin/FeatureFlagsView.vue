<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getFeatureFlags, upsertFeatureFlag, type FeatureFlag } from '@/api/admin'
import { useToast } from '@/composables/useToast'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'

const toast = useToast()
const items = ref<FeatureFlag[]>([])
const loading = ref(false)

const newKey = ref('')
const newDesc = ref('')
const newEnabled = ref(false)

async function fetchList() {
  loading.value = true
  try {
    items.value = await getFeatureFlags()
  } finally {
    loading.value = false
  }
}

async function toggle(item: FeatureFlag) {
  await upsertFeatureFlag({ key: item.key, isEnabled: !item.isEnabled })
  toast.success(`${item.key} 已${!item.isEnabled ? '开启' : '关闭'}`)
  await fetchList()
}

async function addFlag() {
  const key = newKey.value.trim()
  if (!key) {
    toast.warning('请输入 Key')
    return
  }
  await upsertFeatureFlag({ key, isEnabled: newEnabled.value, description: newDesc.value || undefined })
  toast.success('已保存')
  newKey.value = ''
  newDesc.value = ''
  newEnabled.value = false
  await fetchList()
}

onMounted(fetchList)
</script>

<template>
  <div class="p-6 space-y-4">
    <h1 class="text-xl font-semibold">功能开关</h1>

    <div class="rounded border bg-white p-3 space-y-2">
      <div class="text-sm font-semibold">新增 / 更新</div>
      <div class="flex flex-wrap gap-2 items-center">
        <input v-model="newKey" class="border px-2 py-1 rounded text-sm w-64" placeholder="key (如 auto-plot-thread-tracking)" />
        <input v-model="newDesc" class="border px-2 py-1 rounded text-sm flex-1 min-w-64" placeholder="说明（可选）" />
        <label class="text-sm flex items-center gap-1">
          <input v-model="newEnabled" type="checkbox" /> 开启
        </label>
        <AppButton @click="addFlag">保存</AppButton>
      </div>
    </div>

    <div class="rounded border bg-white">
      <table class="w-full text-sm">
        <thead class="bg-gray-50">
          <tr class="text-left text-gray-600">
            <th class="px-3 py-2">Key</th>
            <th>说明</th>
            <th>状态</th>
            <th>更新时间</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="f in items" :key="f.key" class="border-t hover:bg-gray-50">
            <td class="px-3 py-2 font-mono">{{ f.key }}</td>
            <td class="text-gray-600">{{ f.description ?? '-' }}</td>
            <td>
              <span :class="f.isEnabled ? 'text-green-600' : 'text-gray-500'">
                {{ f.isEnabled ? '开启' : '关闭' }}
              </span>
            </td>
            <td class="text-gray-500">{{ new Date(f.updatedAt).toLocaleString() }}</td>
            <td>
              <AppButton size="sm" variant="secondary" @click="toggle(f)">
                {{ f.isEnabled ? '关闭' : '开启' }}
              </AppButton>
            </td>
          </tr>
        </tbody>
      </table>
      <AppEmpty v-if="!loading && items.length === 0" />
    </div>
  </div>
</template>
