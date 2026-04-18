<script setup lang="ts"></script>

<template>
  <div class="not-found flex-center flex-col min-h-screen">
    <h1 class="text-6xl font-bold text-gray-300">404</h1>
    <p class="text-xl mt-4 text-gray-500">页面不存在</p>
    <router-link to="/" class="mt-8 px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600">
      返回首页
    </router-link>
  </div>
</template>
