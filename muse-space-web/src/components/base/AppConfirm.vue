<script setup lang="ts">
import AppModal from './AppModal.vue'
import AppButton from './AppButton.vue'

interface Props {
  modelValue: boolean
  title?: string
  message?: string
  confirmText?: string
  cancelText?: string
  variant?: 'danger' | 'primary'
  loading?: boolean
}

withDefaults(defineProps<Props>(), {
  title: '确认操作',
  message: '确定要执行此操作吗？',
  confirmText: '确认',
  cancelText: '取消',
  variant: 'primary',
  loading: false,
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: []
}>()
</script>

<template>
  <AppModal
    :model-value="modelValue"
    :title="title"
    width="400px"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <p class="confirm-message">{{ message }}</p>
    <template #footer>
      <AppButton variant="secondary" @click="$emit('update:modelValue', false)">
        {{ cancelText }}
      </AppButton>
      <AppButton
        :variant="variant === 'danger' ? 'danger' : 'primary'"
        :loading="loading"
        @click="$emit('confirm')"
      >
        {{ confirmText }}
      </AppButton>
    </template>
  </AppModal>
</template>

<style scoped>
.confirm-message {
  font-size: 14px;
  color: var(--color-text-primary);
  line-height: 1.6;
  margin: 0;
}
</style>
